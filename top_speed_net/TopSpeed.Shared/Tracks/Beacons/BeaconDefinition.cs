using System;
using System.Collections.Generic;
using TopSpeed.Tracks.Areas;

namespace TopSpeed.Tracks.Beacons
{
    public sealed class TrackBeaconDefinition
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public TrackBeaconDefinition(
            string id,
            TrackBeaconType type,
            float x,
            float y,
            float z,
            string? name = null,
            string? nameSecondary = null,
            string? sectorId = null,
            string? geometryId = null,
            float? orientationDegrees = null,
            float? activationRadiusMeters = null,
            TrackBeaconRole role = TrackBeaconRole.Undefined,
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
                throw new ArgumentException("Beacon id is required.", nameof(id));
            if (volumeMinY.HasValue && volumeMaxY.HasValue && volumeMaxY.Value <= volumeMinY.Value)
                throw new ArgumentOutOfRangeException(nameof(volumeMaxY), "Beacon volume max_y must be greater than min_y.");

            Id = id.Trim();
            Type = type;
            Role = role;
            X = x;
            Y = y;
            Z = z;

            var trimmedName = name?.Trim();
            Name = string.IsNullOrWhiteSpace(trimmedName) ? null : trimmedName;
            var trimmedSecondary = nameSecondary?.Trim();
            NameSecondary = string.IsNullOrWhiteSpace(trimmedSecondary) ? null : trimmedSecondary;

            var trimmedSector = sectorId?.Trim();
            SectorId = string.IsNullOrWhiteSpace(trimmedSector) ? null : trimmedSector;
            var trimmedGeometry = geometryId?.Trim();
            GeometryId = string.IsNullOrWhiteSpace(trimmedGeometry) ? null : trimmedGeometry;

            OrientationDegrees = orientationDegrees;
            ActivationRadiusMeters = activationRadiusMeters.HasValue
                ? Math.Max(0.1f, activationRadiusMeters.Value)
                : null;
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
        public TrackBeaconType Type { get; }
        public TrackBeaconRole Role { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public string? Name { get; }
        public string? NameSecondary { get; }
        public string? SectorId { get; }
        public string? GeometryId { get; }
        public float? OrientationDegrees { get; }
        public float? ActivationRadiusMeters { get; }
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
