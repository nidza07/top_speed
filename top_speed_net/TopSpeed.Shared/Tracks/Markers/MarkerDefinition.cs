using System;
using System.Collections.Generic;
using TopSpeed.Tracks.Areas;

namespace TopSpeed.Tracks.Markers
{
    public sealed class TrackMarkerDefinition
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public TrackMarkerDefinition(
            string id,
            TrackMarkerType type,
            float x,
            float y,
            float z,
            string? name = null,
            string? geometryId = null,
            float? headingDegrees = null,
            IReadOnlyDictionary<string, string>? metadata = null,
            float? volumeThicknessMeters = null,
            float? volumeOffsetMeters = null,
            float? volumeMinY = null,
            float? volumeMaxY = null,
            TrackAreaVolumeMode volumeMode = TrackAreaVolumeMode.LocalBand,
            TrackAreaVolumeOffsetMode volumeOffsetMode = TrackAreaVolumeOffsetMode.Bottom,
            TrackAreaVolumeSpace volumeOffsetSpace = TrackAreaVolumeSpace.Inherit,
            TrackAreaVolumeSpace volumeMinMaxSpace = TrackAreaVolumeSpace.Inherit)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Marker id is required.", nameof(id));
            if (volumeMinY.HasValue && volumeMaxY.HasValue && volumeMaxY.Value <= volumeMinY.Value)
                throw new ArgumentOutOfRangeException(nameof(volumeMaxY), "Marker volume max_y must be greater than min_y.");

            Id = id.Trim();
            Type = type;
            X = x;
            Y = y;
            Z = z;
            var trimmedName = name?.Trim();
            Name = string.IsNullOrWhiteSpace(trimmedName) ? null : trimmedName;
            var trimmedGeometry = geometryId?.Trim();
            GeometryId = string.IsNullOrWhiteSpace(trimmedGeometry) ? null : trimmedGeometry;
            HeadingDegrees = headingDegrees;
            Metadata = NormalizeMetadata(metadata);
            VolumeThicknessMeters = volumeThicknessMeters;
            VolumeOffsetMeters = volumeOffsetMeters;
            VolumeMinY = volumeMinY;
            VolumeMaxY = volumeMaxY;
            VolumeMode = volumeMode;
            VolumeOffsetMode = volumeOffsetMode;
            VolumeOffsetSpace = volumeOffsetSpace;
            VolumeMinMaxSpace = volumeMinMaxSpace;
        }

        public string Id { get; }
        public TrackMarkerType Type { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public string? Name { get; }
        public string? GeometryId { get; }
        public float? HeadingDegrees { get; }
        public IReadOnlyDictionary<string, string> Metadata { get; }
        public float? VolumeThicknessMeters { get; }
        public float? VolumeOffsetMeters { get; }
        public float? VolumeMinY { get; }
        public float? VolumeMaxY { get; }
        public TrackAreaVolumeMode VolumeMode { get; }
        public TrackAreaVolumeOffsetMode VolumeOffsetMode { get; }
        public TrackAreaVolumeSpace VolumeOffsetSpace { get; }
        public TrackAreaVolumeSpace VolumeMinMaxSpace { get; }

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
