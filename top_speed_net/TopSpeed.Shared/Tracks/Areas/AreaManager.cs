using System;
using System.Collections.Generic;
using System.Numerics;
using TopSpeed.Tracks.Geometry;
using TopSpeed.Tracks.Volumes;

namespace TopSpeed.Tracks.Areas
{
    public sealed class TrackAreaManager
    {
        private readonly Dictionary<string, GeometryDefinition> _geometries;
        private readonly Dictionary<string, IReadOnlyList<Vector2>> _geometryPoints2D;
        private readonly Dictionary<string, MeshContainment> _meshContainment;
        private readonly TrackVolumeManager? _volumeManager;
        private readonly Dictionary<string, TrackAreaDefinition> _areasById;
        private readonly List<TrackAreaDefinition> _areas;

        public TrackAreaManager(
            IEnumerable<GeometryDefinition> geometries,
            IEnumerable<TrackAreaDefinition> areas,
            IEnumerable<TrackVolumeDefinition>? volumes = null)
        {
            _geometries = new Dictionary<string, GeometryDefinition>(StringComparer.OrdinalIgnoreCase);
            _geometryPoints2D = new Dictionary<string, IReadOnlyList<Vector2>>(StringComparer.OrdinalIgnoreCase);
            _meshContainment = new Dictionary<string, MeshContainment>(StringComparer.OrdinalIgnoreCase);
            _volumeManager = volumes == null ? null : new TrackVolumeManager(volumes, geometries);
            _areasById = new Dictionary<string, TrackAreaDefinition>(StringComparer.OrdinalIgnoreCase);
            _areas = new List<TrackAreaDefinition>();

            if (geometries != null)
            {
                foreach (var geometry in geometries)
                    AddGeometry(geometry);
            }

            if (areas != null)
            {
                foreach (var area in areas)
                    AddArea(area);
            }
        }

        public IReadOnlyList<TrackAreaDefinition> Areas => _areas;

        public void AddGeometry(GeometryDefinition geometry)
        {
            if (geometry == null)
                throw new ArgumentNullException(nameof(geometry));
            _geometries[geometry.Id] = geometry;
            _geometryPoints2D[geometry.Id] = ProjectToXZ(geometry.Points);
            if (geometry.Type == GeometryType.Mesh && MeshContainment.TryCreate(geometry, out var containment))
                _meshContainment[geometry.Id] = containment;
        }

        public void AddArea(TrackAreaDefinition area)
        {
            if (area == null)
                throw new ArgumentNullException(nameof(area));
            _areasById[area.Id] = area;
            _areas.Add(area);
        }

        public bool TryGetGeometry(string id, out GeometryDefinition geometry)
        {
            geometry = null!;
            if (string.IsNullOrWhiteSpace(id))
                return false;
            return _geometries.TryGetValue(id.Trim(), out geometry!);
        }

        public bool TryGetArea(string id, out TrackAreaDefinition area)
        {
            area = null!;
            if (string.IsNullOrWhiteSpace(id))
                return false;
            return _areasById.TryGetValue(id.Trim(), out area!);
        }

        public IReadOnlyList<TrackAreaDefinition> FindAreasContaining(Vector2 position)
        {
            if (_areas.Count == 0)
                return Array.Empty<TrackAreaDefinition>();

            var hits = new List<TrackAreaDefinition>();
            foreach (var area in _areas)
            {
                if (Contains(area, position))
                    hits.Add(area);
            }
            return hits;
        }

        public IReadOnlyList<TrackAreaDefinition> FindAreasContaining(Vector3 position)
        {
            if (_areas.Count == 0)
                return Array.Empty<TrackAreaDefinition>();

            var hits = new List<TrackAreaDefinition>();
            foreach (var area in _areas)
            {
                if (Contains(area, position))
                    hits.Add(area);
            }
            return hits;
        }

        public bool Contains(TrackAreaDefinition area, Vector2 position)
        {
            if (area == null)
                return false;
            if (!TryGetGeometry(area.GeometryId, out var geometry))
                return false;
            var width = area.WidthMeters.GetValueOrDefault();
            var centered = IsCenteredClosedWidth(area.Metadata);
            return Contains(geometry, position, width, centered);
        }

        public bool Contains(TrackAreaDefinition area, Vector3 position)
        {
            if (area == null)
                return false;
            if (!string.IsNullOrWhiteSpace(area.VolumeId) &&
                _volumeManager != null &&
                _volumeManager.Contains(area.VolumeId!, position))
            {
                if (HasVolumeOverrides(area) && TryGetVolumeBounds(area, out var minYOverride, out var maxYOverride))
                    return position.Y >= minYOverride && position.Y <= maxYOverride;
                return true;
            }
            if (!TryGetGeometry(area.GeometryId, out var geometry))
                return false;

            if (geometry.Type == GeometryType.Mesh)
            {
                if (area.VolumeMode != TrackAreaVolumeMode.ClosedMesh)
                    return false;
                if (!TryGetMeshContainment(geometry.Id, out var mesh) || !mesh.IsClosed)
                    return false;
                if (!mesh.Contains(position, MeshContainment.DefaultSurfaceEpsilon))
                    return false;
                if (HasVolumeOverrides(area) && TryGetVolumeBounds(area, out var minY, out var maxY))
                    return position.Y >= minY && position.Y <= maxY;
                return true;
            }

            if (!Contains(area, new Vector2(position.X, position.Z)))
                return false;
            if (!TryGetVolumeBounds(area, out var minY2, out var maxY2))
                return false;
            return position.Y >= minY2 && position.Y <= maxY2;
        }

        public bool ContainsGeometry(string geometryId, Vector2 position, float? widthMeters = null)
        {
            if (!TryGetGeometry(geometryId, out var geometry))
                return false;
            var width = widthMeters.GetValueOrDefault();
            return Contains(geometry, position, width, false);
        }

        internal bool TryGetMeshContainment(string geometryId, out MeshContainment containment)
        {
            containment = null!;
            if (string.IsNullOrWhiteSpace(geometryId))
                return false;
            return _meshContainment.TryGetValue(geometryId.Trim(), out containment!);
        }

        internal bool TryGetVolume(string volumeId, out TrackVolume volume)
        {
            volume = null!;
            if (string.IsNullOrWhiteSpace(volumeId) || _volumeManager == null)
                return false;
            return _volumeManager.TryGetVolume(volumeId.Trim(), out volume!);
        }

        public bool ContainsTrackArea(Vector2 position)
        {
            if (_areas.Count == 0)
                return false;

            foreach (var area in _areas)
            {
                if (area == null)
                    continue;
                if (area.Type == TrackAreaType.Boundary || area.Type == TrackAreaType.OffTrack)
                    continue;
                if (area.Type == TrackAreaType.Start || area.Type == TrackAreaType.Finish ||
                    area.Type == TrackAreaType.Checkpoint || area.Type == TrackAreaType.Intersection)
                    continue;
                if (Contains(area, position))
                    return true;
            }

            return false;
        }

        public bool ContainsTrackArea(Vector3 position)
        {
            if (_areas.Count == 0)
                return false;

            foreach (var area in _areas)
            {
                if (area == null)
                    continue;
                if (area.Type == TrackAreaType.Boundary || area.Type == TrackAreaType.OffTrack)
                    continue;
                if (area.Type == TrackAreaType.Start || area.Type == TrackAreaType.Finish ||
                    area.Type == TrackAreaType.Checkpoint || area.Type == TrackAreaType.Intersection)
                    continue;
                if (Contains(area, position))
                    return true;
            }

            return false;
        }

        private bool Contains(GeometryDefinition geometry, Vector2 position, float widthMeters, bool closedCentered)
        {
            if (geometry == null)
                return false;
            if (!_geometryPoints2D.TryGetValue(geometry.Id, out var points2D))
                points2D = ProjectToXZ(geometry.Points);

            switch (geometry.Type)
            {
                case GeometryType.Polygon:
                    return ContainsPolygonPath(points2D, position, widthMeters, closedCentered);
                case GeometryType.Polyline:
                case GeometryType.Spline:
                    return ContainsPolylinePath(points2D, position, widthMeters, closedCentered);
                case GeometryType.Mesh:
                case GeometryType.Undefined:
                default:
                    return false;
            }
        }

        private static bool ContainsPolygon(IReadOnlyList<Vector2> points, Vector2 position)
        {
            if (points == null || points.Count < 3)
                return false;

            var inside = false;
            var j = points.Count - 1;
            for (var i = 0; i < points.Count; i++)
            {
                var xi = points[i].X;
                var zi = points[i].Y;
                var xj = points[j].X;
                var zj = points[j].Y;

                var intersect = ((zi > position.Y) != (zj > position.Y)) &&
                                (position.X < (xj - xi) * (position.Y - zi) / (zj - zi + float.Epsilon) + xi);
                if (intersect)
                    inside = !inside;
                j = i;
            }

            return inside;
        }

        private static bool ContainsPolygonPath(
            IReadOnlyList<Vector2> points,
            Vector2 position,
            float widthMeters,
            bool closedCentered)
        {
            if (points == null || points.Count < 3)
                return false;

            var width = Math.Abs(widthMeters);
            if (width <= 0f)
                return ContainsPolygon(points, position);

            if (closedCentered)
            {
                var radius = width * 0.5f;
                return DistanceToPolylineSquared(points, position, true) <= (radius * radius);
            }

            if (!ContainsPolygon(points, position))
                return false;
            return DistanceToPolylineSquared(points, position, true) <= (width * width);
        }

        private static bool ContainsPolylinePath(
            IReadOnlyList<Vector2> points,
            Vector2 position,
            float widthMeters,
            bool closedCentered)
        {
            if (points == null || points.Count < 2)
                return false;

            var width = Math.Abs(widthMeters);
            if (width <= 0f)
                return false;

            var closed = IsClosedPolyline(points);
            if (!closed)
            {
                var radius = width * 0.5f;
                return DistanceToPolylineSquared(points, position, false) <= (radius * radius);
            }

            if (closedCentered)
            {
                var radius = width * 0.5f;
                return DistanceToPolylineSquared(points, position, true) <= (radius * radius);
            }

            if (!ContainsPolygon(points, position))
                return false;
            return DistanceToPolylineSquared(points, position, true) <= (width * width);
        }

        private static float DistanceToSegmentSquared(Vector2 a, Vector2 b, Vector2 p)
        {
            var ab = b - a;
            var ap = p - a;
            var abLenSq = Vector2.Dot(ab, ab);
            if (abLenSq <= float.Epsilon)
                return Vector2.Dot(ap, ap);

            var t = Vector2.Dot(ap, ab) / abLenSq;
            if (t < 0f)
                t = 0f;
            else if (t > 1f)
                t = 1f;

            var closest = a + ab * t;
            var delta = p - closest;
            return Vector2.Dot(delta, delta);
        }

        private static float DistanceToPolylineSquared(IReadOnlyList<Vector2> points, Vector2 position, bool closed)
        {
            if (points == null || points.Count < 2)
                return float.MaxValue;

            var count = points.Count;
            var lastIndex = count - 1;
            var lastEqualsFirst = count > 1 && Vector2.DistanceSquared(points[0], points[lastIndex]) <= 0.0001f;
            var segmentCount = lastEqualsFirst ? lastIndex : count - 1;

            var best = float.MaxValue;
            for (var i = 0; i < segmentCount; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                var dist = DistanceToSegmentSquared(a, b, position);
                if (dist < best)
                    best = dist;
            }

            if (closed && !lastEqualsFirst)
            {
                var dist = DistanceToSegmentSquared(points[lastIndex], points[0], position);
                if (dist < best)
                    best = dist;
            }

            return best;
        }

        private static bool IsClosedPolyline(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count < 3)
                return false;
            return Vector2.DistanceSquared(points[0], points[points.Count - 1]) <= 0.0001f;
        }

        private static bool TryGetVolumeBounds(TrackAreaDefinition area, out float minY, out float maxY)
        {
            minY = 0f;
            maxY = 0f;
            if (area == null)
                return false;

            var thickness = area.VolumeThicknessMeters ?? area.HeightMeters;
            if (thickness <= 0f)
                thickness = area.HeightMeters;
            if (thickness <= 0f)
                return false;

            return VolumeBounds.TryResolve(
                area.ElevationMeters,
                area.VolumeMode,
                area.VolumeOffsetMode,
                area.VolumeOffsetSpace,
                area.VolumeMinMaxSpace,
                thickness,
                area.VolumeOffsetMeters,
                area.VolumeMinY,
                area.VolumeMaxY,
                out minY,
                out maxY);
        }

        private static bool HasVolumeOverrides(TrackAreaDefinition area)
        {
            if (area == null)
                return false;
            return area.VolumeThicknessMeters.HasValue ||
                   area.VolumeOffsetMeters.HasValue ||
                   area.VolumeMinY.HasValue ||
                   area.VolumeMaxY.HasValue;
        }

        private static IReadOnlyList<Vector2> ProjectToXZ(IReadOnlyList<Vector3> points)
        {
            if (points == null || points.Count == 0)
                return Array.Empty<Vector2>();

            var projected = new List<Vector2>(points.Count);
            foreach (var point in points)
                projected.Add(new Vector2(point.X, point.Z));
            return projected;
        }

        private static bool IsCenteredClosedWidth(IReadOnlyDictionary<string, string> metadata)
        {
            if (metadata == null || metadata.Count == 0)
                return false;

            if (!TryGetMetadataValue(metadata, out var mode, "width_mode", "path_width_mode", "width_align", "width_alignment"))
                return false;

            var trimmed = mode.Trim().ToLowerInvariant();
            return trimmed.Contains("center") ||
                   trimmed.Contains("centre") ||
                   trimmed.Contains("both") ||
                   trimmed.Contains("sym");
        }

        private static bool TryGetMetadataValue(
            IReadOnlyDictionary<string, string> metadata,
            out string value,
            params string[] keys)
        {
            value = string.Empty;
            if (metadata == null || metadata.Count == 0)
                return false;

            foreach (var key in keys)
            {
                if (metadata.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw))
                {
                    value = raw;
                    return true;
                }
            }

            return false;
        }
    }
}
