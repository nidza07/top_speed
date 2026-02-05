using System;
using TopSpeed.Tracks.Walls;

namespace TopSpeed.Tracks.Materials
{
    public sealed class TrackMaterialDefinition
    {
        public TrackMaterialDefinition(
            string id,
            string? name,
            float absorptionLow,
            float absorptionMid,
            float absorptionHigh,
            float scattering,
            float transmissionLow,
            float transmissionMid,
            float transmissionHigh,
            TrackWallMaterial collisionMaterial)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Material id is required.", nameof(id));

            Id = id.Trim();
            var trimmedName = name?.Trim();
            Name = string.IsNullOrWhiteSpace(trimmedName) ? null : trimmedName;
            AbsorptionLow = Clamp01(absorptionLow);
            AbsorptionMid = Clamp01(absorptionMid);
            AbsorptionHigh = Clamp01(absorptionHigh);
            Scattering = Clamp01(scattering);
            TransmissionLow = Clamp01(transmissionLow);
            TransmissionMid = Clamp01(transmissionMid);
            TransmissionHigh = Clamp01(transmissionHigh);
            CollisionMaterial = collisionMaterial;
        }

        public string Id { get; }
        public string? Name { get; }
        public float AbsorptionLow { get; }
        public float AbsorptionMid { get; }
        public float AbsorptionHigh { get; }
        public float Scattering { get; }
        public float TransmissionLow { get; }
        public float TransmissionMid { get; }
        public float TransmissionHigh { get; }
        public TrackWallMaterial CollisionMaterial { get; }

        private static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;
            if (value > 1f)
                return 1f;
            return value;
        }
    }
}
