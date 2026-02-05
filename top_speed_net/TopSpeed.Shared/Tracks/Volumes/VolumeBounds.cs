using System;
using TopSpeed.Tracks.Areas;

namespace TopSpeed.Tracks.Volumes
{
    public static class VolumeBounds
    {
        public static bool TryResolve(
            float baseY,
            TrackAreaVolumeMode mode,
            TrackAreaVolumeOffsetMode offsetMode,
            TrackAreaVolumeSpace offsetSpace,
            TrackAreaVolumeSpace minMaxSpace,
            float? thicknessMeters,
            float? offsetMeters,
            float? minYOverride,
            float? maxYOverride,
            out float minY,
            out float maxY)
        {
            minY = 0f;
            maxY = 0f;

            var hasMin = minYOverride.HasValue;
            var hasMax = maxYOverride.HasValue;

            var defaultSpace = mode == TrackAreaVolumeMode.WorldBand
                ? TrackAreaVolumeSpace.World
                : TrackAreaVolumeSpace.Local;
            var resolvedOffsetSpace = offsetSpace == TrackAreaVolumeSpace.Inherit ? defaultSpace : offsetSpace;
            var resolvedMinMaxSpace = minMaxSpace == TrackAreaVolumeSpace.Inherit ? defaultSpace : minMaxSpace;

            var minOverride = 0f;
            var maxOverride = 0f;
            if (hasMin)
                minOverride = (resolvedMinMaxSpace == TrackAreaVolumeSpace.Local ? baseY : 0f) + minYOverride!.Value;
            if (hasMax)
                maxOverride = (resolvedMinMaxSpace == TrackAreaVolumeSpace.Local ? baseY : 0f) + maxYOverride!.Value;

            var thickness = thicknessMeters;
            if ((!thickness.HasValue || thickness.Value <= 0f) && hasMin && hasMax)
                thickness = maxOverride - minOverride;

            if (!thickness.HasValue || thickness.Value <= 0f)
                return false;

            var offsetValue = offsetMeters ?? (resolvedOffsetSpace == TrackAreaVolumeSpace.World ? baseY : 0f);
            var offsetBase = offsetValue + (resolvedOffsetSpace == TrackAreaVolumeSpace.Local ? baseY : 0f);

            minY = offsetBase;
            switch (offsetMode)
            {
                case TrackAreaVolumeOffsetMode.Center:
                    minY = offsetBase - (thickness.Value * 0.5f);
                    break;
                case TrackAreaVolumeOffsetMode.Top:
                    minY = offsetBase - thickness.Value;
                    break;
                case TrackAreaVolumeOffsetMode.Bottom:
                default:
                    minY = offsetBase;
                    break;
            }
            maxY = minY + thickness.Value;

            if (hasMin)
                minY = minOverride;
            if (hasMax)
                maxY = maxOverride;
            if (hasMin && !hasMax)
                maxY = minY + thickness.Value;
            if (!hasMin && hasMax)
                minY = maxY - thickness.Value;

            return maxY > minY;
        }
    }
}
