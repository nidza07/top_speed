using System;
using System.Collections.Generic;
using System.Numerics;
using TopSpeed.Tracks.Geometry;

namespace TopSpeed.Tracks.Volumes
{
    public sealed class TrackVolumeManager
    {
        private readonly Dictionary<string, TrackVolumeDefinition> _definitions;
        private readonly Dictionary<string, TrackVolume> _volumes;

        public TrackVolumeManager(
            IEnumerable<TrackVolumeDefinition> volumes,
            IEnumerable<GeometryDefinition> geometries)
        {
            _definitions = new Dictionary<string, TrackVolumeDefinition>(StringComparer.OrdinalIgnoreCase);
            _volumes = new Dictionary<string, TrackVolume>(StringComparer.OrdinalIgnoreCase);

            var geometryLookup = new Dictionary<string, GeometryDefinition>(StringComparer.OrdinalIgnoreCase);
            if (geometries != null)
            {
                foreach (var geometry in geometries)
                {
                    if (geometry == null || string.IsNullOrWhiteSpace(geometry.Id))
                        continue;
                    geometryLookup[geometry.Id.Trim()] = geometry;
                }
            }

            if (volumes == null)
                return;

            foreach (var volume in volumes)
            {
                if (volume == null || string.IsNullOrWhiteSpace(volume.Id))
                    continue;
                _definitions[volume.Id] = volume;
                if (TrackVolume.TryCreate(volume, geometryLookup, out var instance))
                    _volumes[volume.Id] = instance;
            }
        }

        public IReadOnlyCollection<TrackVolumeDefinition> Definitions => _definitions.Values;

        public bool TryGetDefinition(string id, out TrackVolumeDefinition definition)
        {
            definition = null!;
            if (string.IsNullOrWhiteSpace(id))
                return false;
            return _definitions.TryGetValue(id.Trim(), out definition!);
        }

        public bool Contains(string id, Vector3 position)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;
            return _volumes.TryGetValue(id.Trim(), out var volume) && volume.Contains(position);
        }

        internal bool TryGetVolume(string id, out TrackVolume volume)
        {
            volume = null!;
            if (string.IsNullOrWhiteSpace(id))
                return false;
            return _volumes.TryGetValue(id.Trim(), out volume!);
        }
    }

    internal sealed class TrackVolume
    {
        private const float SurfaceEpsilon = 0.01f;

        private readonly TrackVolumeDefinition _definition;
        private readonly TrackVolumeType _type;
        private readonly Vector3 _center;
        private readonly Vector3 _halfSize;
        private readonly float _radius;
        private readonly float _height;
        private readonly float _minY;
        private readonly float _maxY;
        private readonly bool _hasMinMax;
        private readonly bool _hasRotation;
        private readonly Matrix4x4 _inverseRotation;
        private readonly Vector2[]? _prismPolygon;
        private readonly MeshContainment? _mesh;

        private TrackVolume(
            TrackVolumeDefinition definition,
            Vector3 center,
            Vector3 halfSize,
            float radius,
            float height,
            bool hasRotation,
            Matrix4x4 inverseRotation,
            Vector2[]? prismPolygon,
            float minY,
            float maxY,
            bool hasMinMax,
            MeshContainment? mesh)
        {
            _definition = definition;
            _type = definition.Type;
            _center = center;
            _halfSize = halfSize;
            _radius = radius;
            _height = height;
            _hasRotation = hasRotation;
            _inverseRotation = inverseRotation;
            _prismPolygon = prismPolygon;
            _minY = minY;
            _maxY = maxY;
            _hasMinMax = hasMinMax;
            _mesh = mesh;
        }

        public TrackVolumeDefinition Definition => _definition;

        public bool Contains(Vector3 point)
        {
            if (_type == TrackVolumeType.Mesh)
                return _mesh != null && _mesh.Contains(point, SurfaceEpsilon);

            var local = point - _center;
            if (_hasRotation)
                local = Vector3.Transform(local, _inverseRotation);

            switch (_type)
            {
                case TrackVolumeType.Box:
                    return Math.Abs(local.X) <= _halfSize.X &&
                           Math.Abs(local.Y) <= _halfSize.Y &&
                           Math.Abs(local.Z) <= _halfSize.Z;
                case TrackVolumeType.Sphere:
                    return local.LengthSquared() <= (_radius * _radius);
                case TrackVolumeType.Cylinder:
                    return Math.Abs(local.Y) <= (_height * 0.5f) &&
                           ((local.X * local.X) + (local.Z * local.Z)) <= (_radius * _radius);
                case TrackVolumeType.Capsule:
                    return CapsuleContains(local, _radius, _height);
                case TrackVolumeType.Prism:
                    if (_prismPolygon == null || _prismPolygon.Length < 3)
                        return false;
                    if (_hasMinMax && (local.Y < _minY || local.Y > _maxY))
                        return false;
                    return ContainsPolygon(_prismPolygon, new Vector2(local.X, local.Z));
                default:
                    return false;
            }
        }

        public static bool TryCreate(
            TrackVolumeDefinition definition,
            IReadOnlyDictionary<string, GeometryDefinition> geometries,
            out TrackVolume volume)
        {
            volume = null!;
            if (definition == null)
                return false;

            var rotation = definition.RotationDegrees;
            var hasRotation = Math.Abs(rotation.X) > 0.0001f ||
                              Math.Abs(rotation.Y) > 0.0001f ||
                              Math.Abs(rotation.Z) > 0.0001f;
            var inverseRotation = Matrix4x4.Identity;
            if (hasRotation)
            {
                var yaw = rotation.Y * (float)Math.PI / 180f;
                var pitch = rotation.X * (float)Math.PI / 180f;
                var roll = rotation.Z * (float)Math.PI / 180f;
                var matrix = Matrix4x4.CreateFromYawPitchRoll(yaw, pitch, roll);
                Matrix4x4.Invert(matrix, out inverseRotation);
            }

            switch (definition.Type)
            {
                case TrackVolumeType.Box:
                    if (definition.Size.X <= 0f || definition.Size.Y <= 0f || definition.Size.Z <= 0f)
                        return false;
                    volume = new TrackVolume(
                        definition,
                        definition.Center,
                        definition.Size * 0.5f,
                        0f,
                        0f,
                        hasRotation,
                        inverseRotation,
                        null,
                        0f,
                        0f,
                        false,
                        null);
                    return true;
                case TrackVolumeType.Sphere:
                    if (definition.Radius <= 0f)
                        return false;
                    volume = new TrackVolume(
                        definition,
                        definition.Center,
                        Vector3.Zero,
                        definition.Radius,
                        0f,
                        hasRotation,
                        inverseRotation,
                        null,
                        0f,
                        0f,
                        false,
                        null);
                    return true;
                case TrackVolumeType.Cylinder:
                    if (definition.Radius <= 0f || definition.Height <= 0f)
                        return false;
                    volume = new TrackVolume(
                        definition,
                        definition.Center,
                        Vector3.Zero,
                        definition.Radius,
                        definition.Height,
                        hasRotation,
                        inverseRotation,
                        null,
                        0f,
                        0f,
                        false,
                        null);
                    return true;
                case TrackVolumeType.Capsule:
                    if (definition.Radius <= 0f || definition.Height <= 0f)
                        return false;
                    volume = new TrackVolume(
                        definition,
                        definition.Center,
                        Vector3.Zero,
                        definition.Radius,
                        definition.Height,
                        hasRotation,
                        inverseRotation,
                        null,
                        0f,
                        0f,
                        false,
                        null);
                    return true;
                case TrackVolumeType.Prism:
                    if (string.IsNullOrWhiteSpace(definition.GeometryId) ||
                        geometries == null ||
                        !geometries.TryGetValue(definition.GeometryId!, out var geometry) ||
                        geometry.Type != GeometryType.Polygon ||
                        geometry.Points == null ||
                        geometry.Points.Count < 3)
                        return false;
                    var center = definition.HasCenter ? definition.Center : ComputeCenter(geometry.Points);
                    var polygon = BuildPolygon(geometry.Points, center, hasRotation, inverseRotation);
                    if (polygon.Length < 3)
                        return false;
                    volume = new TrackVolume(
                        definition,
                        center,
                        Vector3.Zero,
                        0f,
                        0f,
                        hasRotation,
                        inverseRotation,
                        polygon,
                        definition.MinY ?? 0f,
                        definition.MaxY ?? 0f,
                        definition.MinY.HasValue || definition.MaxY.HasValue,
                        null);
                    return true;
                case TrackVolumeType.Mesh:
                    if (string.IsNullOrWhiteSpace(definition.GeometryId) ||
                        geometries == null ||
                        !geometries.TryGetValue(definition.GeometryId!, out var meshGeometry))
                        return false;
                    if (!MeshContainment.TryCreate(meshGeometry, out var mesh))
                        return false;
                    volume = new TrackVolume(
                        definition,
                        definition.Center,
                        Vector3.Zero,
                        0f,
                        0f,
                        false,
                        Matrix4x4.Identity,
                        null,
                        0f,
                        0f,
                        false,
                        mesh);
                    return true;
                default:
                    return false;
            }
        }

        private static Vector2[] BuildPolygon(
            IReadOnlyList<Vector3> points,
            Vector3 center,
            bool hasRotation,
            Matrix4x4 inverseRotation)
        {
            var list = new List<Vector2>(points.Count);
            for (var i = 0; i < points.Count; i++)
            {
                var local = points[i] - center;
                if (hasRotation)
                    local = Vector3.Transform(local, inverseRotation);
                list.Add(new Vector2(local.X, local.Z));
            }

            if (list.Count > 2 && Vector2.DistanceSquared(list[0], list[list.Count - 1]) <= 0.0001f)
                list.RemoveAt(list.Count - 1);

            return list.ToArray();
        }

        private static Vector3 ComputeCenter(IReadOnlyList<Vector3> points)
        {
            if (points == null || points.Count == 0)
                return Vector3.Zero;
            var sum = Vector3.Zero;
            for (var i = 0; i < points.Count; i++)
                sum += points[i];
            return sum / points.Count;
        }

        private static bool CapsuleContains(Vector3 local, float radius, float height)
        {
            var half = Math.Max(0f, (height * 0.5f) - radius);
            var clampedY = local.Y;
            if (clampedY > half)
                clampedY = half;
            else if (clampedY < -half)
                clampedY = -half;
            var closest = new Vector3(0f, clampedY, 0f);
            var delta = local - closest;
            return delta.LengthSquared() <= (radius * radius);
        }

        private static bool ContainsPolygon(IReadOnlyList<Vector2> points, Vector2 position)
        {
            var inside = false;
            var j = points.Count - 1;
            for (var i = 0; i < points.Count; i++)
            {
                var pi = points[i];
                var pj = points[j];
                var intersect = ((pi.Y > position.Y) != (pj.Y > position.Y)) &&
                                (position.X < (pj.X - pi.X) * (position.Y - pi.Y) / (pj.Y - pi.Y + float.Epsilon) + pi.X);
                if (intersect)
                    inside = !inside;
                j = i;
            }
            return inside;
        }
    }
}
