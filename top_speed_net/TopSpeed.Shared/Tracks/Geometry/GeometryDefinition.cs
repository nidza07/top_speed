using System;
using System.Collections.Generic;
using System.Numerics;

namespace TopSpeed.Tracks.Geometry
{
    public sealed class GeometryDefinition
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public GeometryDefinition(
            string id,
            GeometryType type,
            IReadOnlyList<Vector3> points,
            IReadOnlyList<int>? triangleIndices = null,
            string? name = null,
            IReadOnlyDictionary<string, string>? metadata = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Geometry id is required.", nameof(id));

            Id = id.Trim();
            Type = type;
            Points = points ?? Array.Empty<Vector3>();
            TriangleIndices = triangleIndices ?? Array.Empty<int>();
            var trimmedName = name?.Trim();
            Name = string.IsNullOrWhiteSpace(trimmedName) ? null : trimmedName;
            Metadata = NormalizeMetadata(metadata);
        }

        public string Id { get; }
        public GeometryType Type { get; }
        public IReadOnlyList<Vector3> Points { get; }
        public IReadOnlyList<int> TriangleIndices { get; }
        public string? Name { get; }
        public IReadOnlyDictionary<string, string> Metadata { get; }

        private static IReadOnlyDictionary<string, string> NormalizeMetadata(IReadOnlyDictionary<string, string>? metadata)
        {
            if (metadata == null || metadata.Count == 0)
                return EmptyMetadata;
            var copy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in metadata)
                copy[pair.Key] = pair.Value;
            return copy;
        }
    }
}
