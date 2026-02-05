using System;
using System.Collections.Generic;

namespace TopSpeed.Tracks.Surfaces
{
    public sealed class TrackSurfaceDefinition
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public TrackSurfaceDefinition(
            string id,
            TrackSurfaceType type,
            string? geometryId,
            string? profileId,
            string? bankId,
            int layer,
            float? resolutionMeters,
            string? materialId,
            string? name,
            IReadOnlyDictionary<string, string>? metadata = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Surface id is required.", nameof(id));

            Id = id.Trim();
            Type = type;
            GeometryId = string.IsNullOrWhiteSpace(geometryId) ? null : geometryId!.Trim();
            ProfileId = string.IsNullOrWhiteSpace(profileId) ? null : profileId!.Trim();
            BankId = string.IsNullOrWhiteSpace(bankId) ? null : bankId!.Trim();
            Layer = layer;
            ResolutionMeters = resolutionMeters.HasValue ? Math.Max(0.1f, resolutionMeters.Value) : (float?)null;
            MaterialId = string.IsNullOrWhiteSpace(materialId) ? null : materialId!.Trim();
            var trimmedName = name?.Trim();
            Name = string.IsNullOrWhiteSpace(trimmedName) ? null : trimmedName;
            Metadata = NormalizeMetadata(metadata);
        }

        public string Id { get; }
        public TrackSurfaceType Type { get; }
        public string? GeometryId { get; }
        public string? ProfileId { get; }
        public string? BankId { get; }
        public int Layer { get; }
        public float? ResolutionMeters { get; }
        public string? MaterialId { get; }
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
