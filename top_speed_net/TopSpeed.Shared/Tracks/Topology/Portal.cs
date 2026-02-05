using System;
using TopSpeed.Tracks.Areas;

namespace TopSpeed.Tracks.Topology
{
    public sealed class PortalDefinition
    {
        public PortalDefinition(
            string id,
            string sectorId,
            float x,
            float y,
            float z,
            float widthMeters,
            float? entryHeadingDegrees,
            float? exitHeadingDegrees,
            PortalRole role,
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
                throw new ArgumentException("Portal id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(sectorId))
                throw new ArgumentException("Sector id is required.", nameof(sectorId));
            if (volumeMinY.HasValue && volumeMaxY.HasValue && volumeMaxY.Value <= volumeMinY.Value)
                throw new ArgumentOutOfRangeException(nameof(volumeMaxY), "Portal volume max_y must be greater than min_y.");

            Id = id.Trim();
            SectorId = sectorId.Trim();
            X = x;
            Y = y;
            Z = z;
            WidthMeters = widthMeters;
            EntryHeadingDegrees = entryHeadingDegrees;
            ExitHeadingDegrees = exitHeadingDegrees;
            Role = role;
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
        public string SectorId { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float WidthMeters { get; }
        public float? EntryHeadingDegrees { get; }
        public float? ExitHeadingDegrees { get; }
        public PortalRole Role { get; }
        public float? VolumeThicknessMeters { get; }
        public float? VolumeOffsetMeters { get; }
        public float? VolumeMinY { get; }
        public float? VolumeMaxY { get; }
        public TrackAreaVolumeMode VolumeMode { get; }
        public TrackAreaVolumeOffsetMode VolumeOffsetMode { get; }
        public TrackAreaVolumeSpace VolumeOffsetSpace { get; }
        public TrackAreaVolumeSpace VolumeMinMaxSpace { get; }
    }
}
