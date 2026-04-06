namespace TopSpeed.Physics.Powertrain
{
    public readonly struct ResistanceEnvironment
    {
        public static readonly ResistanceEnvironment Calm = new(1.225f, 0f, 0f, 1f);

        public ResistanceEnvironment(
            float airDensityKgPerM3,
            float longitudinalWindMps,
            float lateralWindMps,
            float draftingFactor)
        {
            AirDensityKgPerM3 = airDensityKgPerM3 > 0f ? airDensityKgPerM3 : 1.225f;
            LongitudinalWindMps = longitudinalWindMps;
            LateralWindMps = lateralWindMps;
            DraftingFactor = draftingFactor < 0.1f ? 0.1f : draftingFactor;
        }

        public float AirDensityKgPerM3 { get; }
        public float LongitudinalWindMps { get; }
        public float LateralWindMps { get; }
        public float DraftingFactor { get; }
    }
}
