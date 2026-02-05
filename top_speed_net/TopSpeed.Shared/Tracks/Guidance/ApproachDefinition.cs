using System;
using System.Collections.Generic;
using TopSpeed.Tracks.Areas;

namespace TopSpeed.Tracks.Guidance
{
    public sealed class TrackApproachDefinition
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public TrackApproachDefinition(
            string sectorId,
            string? name = null,
            string? entryPortalId = null,
            string? exitPortalId = null,
            float? entryHeadingDegrees = null,
            float? exitHeadingDegrees = null,
            float? widthMeters = null,
            float? lengthMeters = null,
            float? alignmentToleranceDegrees = null,
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
            if (string.IsNullOrWhiteSpace(sectorId))
                throw new ArgumentException("Sector id is required.", nameof(sectorId));
            if (volumeMinY.HasValue && volumeMaxY.HasValue && volumeMaxY.Value <= volumeMinY.Value)
                throw new ArgumentOutOfRangeException(nameof(volumeMaxY), "Approach volume max_y must be greater than min_y.");

            SectorId = sectorId.Trim();
            var trimmedName = name?.Trim();
            Name = string.IsNullOrWhiteSpace(trimmedName) ? null : trimmedName;
            EntryPortalId = string.IsNullOrWhiteSpace(entryPortalId) ? null : entryPortalId!.Trim();
            ExitPortalId = string.IsNullOrWhiteSpace(exitPortalId) ? null : exitPortalId!.Trim();
            EntryHeadingDegrees = entryHeadingDegrees;
            ExitHeadingDegrees = exitHeadingDegrees;
            WidthMeters = widthMeters;
            LengthMeters = lengthMeters;
            AlignmentToleranceDegrees = alignmentToleranceDegrees;
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

        public string SectorId { get; }
        public string? Name { get; }
        public string? EntryPortalId { get; }
        public string? ExitPortalId { get; }
        public float? EntryHeadingDegrees { get; }
        public float? ExitHeadingDegrees { get; }
        public float? WidthMeters { get; }
        public float? LengthMeters { get; }
        public float? AlignmentToleranceDegrees { get; }
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
