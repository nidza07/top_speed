using System;
using System.Collections.Generic;
using System.Numerics;
using TopSpeed.Tracks.Geometry;
using TopSpeed.Tracks.Materials;
using TopSpeed.Tracks.Walls;

namespace TopSpeed.Tracks.Collisions
{
    public readonly struct TrackMeshCollision
    {
        public TrackMeshCollision(
            string geometryId,
            Vector3 position,
            Vector3 normal,
            TrackWallCollisionMode mode,
            TrackWallMaterial material,
            float t)
        {
            GeometryId = geometryId;
            Position = position;
            Normal = normal;
            Mode = mode;
            Material = material;
            T = t;
        }

        public string GeometryId { get; }
        public Vector3 Position { get; }
        public Vector3 Normal { get; }
        public TrackWallCollisionMode Mode { get; }
        public TrackWallMaterial Material { get; }
        public float T { get; }
    }

    public sealed class TrackMeshCollisionManager
    {
        private readonly List<MeshCollider> _colliders;

        public TrackMeshCollisionManager(
            IEnumerable<GeometryDefinition> geometries,
            IEnumerable<TrackMaterialDefinition>? materials = null)
        {
            _colliders = new List<MeshCollider>();
            if (geometries == null)
                return;

            var materialLookup = BuildMaterialLookup(materials);
            foreach (var geometry in geometries)
            {
                if (geometry == null || geometry.Type != GeometryType.Mesh)
                    continue;

                if (!TryResolveCollisionSettings(geometry, materialLookup, out var mode, out var material))
                    continue;

                if (mode == TrackWallCollisionMode.Pass)
                    continue;

                if (!MeshBvh.TryCreate(geometry, out var bvh))
                    continue;

                _colliders.Add(new MeshCollider(geometry.Id, bvh, mode, material));
            }
        }

        public bool HasColliders => _colliders.Count > 0;

        public bool TryGetCollision(Vector3 from, Vector3 to, out TrackMeshCollision collision)
        {
            collision = default;
            if (_colliders.Count == 0)
                return false;

            var bestT = float.MaxValue;
            MeshCollider? best = null;
            MeshBvhHit bestHit = default;

            for (var i = 0; i < _colliders.Count; i++)
            {
                var collider = _colliders[i];
                if (!collider.Bvh.TryIntersectSegment(from, to, out var hit))
                    continue;

                if (hit.T < bestT)
                {
                    bestT = hit.T;
                    best = collider;
                    bestHit = hit;
                }
            }

            if (best == null)
                return false;

            collision = new TrackMeshCollision(
                best.GeometryId,
                bestHit.Position,
                bestHit.Normal,
                best.Mode,
                best.Material,
                bestHit.T);
            return true;
        }

        private static Dictionary<string, TrackMaterialDefinition> BuildMaterialLookup(IEnumerable<TrackMaterialDefinition>? materials)
        {
            var lookup = new Dictionary<string, TrackMaterialDefinition>(StringComparer.OrdinalIgnoreCase);
            if (materials == null)
                return lookup;

            foreach (var material in materials)
            {
                if (material == null || string.IsNullOrWhiteSpace(material.Id))
                    continue;
                lookup[material.Id] = material;
            }

            return lookup;
        }

        private static bool TryResolveCollisionSettings(
            GeometryDefinition geometry,
            Dictionary<string, TrackMaterialDefinition> materialLookup,
            out TrackWallCollisionMode mode,
            out TrackWallMaterial material)
        {
            mode = TrackWallCollisionMode.Block;
            material = TrackWallMaterial.Hard;

            var metadata = geometry.Metadata;
            if (metadata == null || metadata.Count == 0)
                return false;

            var hasCollisionFlag = false;
            if (TryGetString(metadata, out var rawCollision, "collision", "collidable", "collision_mode", "collision_kind", "collision_type"))
            {
                hasCollisionFlag = true;
                if (IsFalse(rawCollision))
                    return false;

                if (TryParseCollisionMode(rawCollision, out var parsedMode))
                {
                    mode = parsedMode;
                    if (mode == TrackWallCollisionMode.Pass)
                        return false;
                }
            }

            if (!hasCollisionFlag)
                return false;

            if (TryGetString(metadata, out var rawMaterial, "collision_material", "collision_mat", "material", "material_id"))
            {
                if (materialLookup.TryGetValue(rawMaterial, out var trackMaterial))
                {
                    material = trackMaterial.CollisionMaterial;
                }
                else if (TryParseWallMaterial(rawMaterial, out var parsedMaterial))
                {
                    material = parsedMaterial;
                }
            }

            return true;
        }

        private static bool TryGetString(
            IReadOnlyDictionary<string, string> metadata,
            out string value,
            params string[] keys)
        {
            value = string.Empty;
            for (var i = 0; i < keys.Length; i++)
            {
                if (metadata.TryGetValue(keys[i], out var raw) && !string.IsNullOrWhiteSpace(raw))
                {
                    value = raw.Trim();
                    return true;
                }
            }
            return false;
        }

        private static bool TryParseCollisionMode(string raw, out TrackWallCollisionMode mode)
        {
            mode = TrackWallCollisionMode.Block;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var normalized = raw.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "block":
                case "blocked":
                case "hard":
                case "solid":
                    mode = TrackWallCollisionMode.Block;
                    return true;
                case "bounce":
                case "soft":
                    mode = TrackWallCollisionMode.Bounce;
                    return true;
                case "pass":
                case "none":
                case "off":
                case "ignore":
                    mode = TrackWallCollisionMode.Pass;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParseWallMaterial(string raw, out TrackWallMaterial material)
        {
            material = TrackWallMaterial.Hard;
            if (string.IsNullOrWhiteSpace(raw))
                return false;
            return Enum.TryParse(raw.Trim(), true, out material);
        }

        private static bool IsFalse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return false;
            var normalized = raw.Trim().ToLowerInvariant();
            return normalized == "0" ||
                   normalized == "false" ||
                   normalized == "no" ||
                   normalized == "off" ||
                   normalized == "none";
        }

        private sealed class MeshCollider
        {
            public MeshCollider(string geometryId, MeshBvh bvh, TrackWallCollisionMode mode, TrackWallMaterial material)
            {
                GeometryId = geometryId;
                Bvh = bvh;
                Mode = mode;
                Material = material;
            }

            public string GeometryId { get; }
            public MeshBvh Bvh { get; }
            public TrackWallCollisionMode Mode { get; }
            public TrackWallMaterial Material { get; }
        }
    }
}
