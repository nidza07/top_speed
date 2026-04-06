namespace TopSpeed.Physics.Surface
{
    public readonly struct SurfaceModifiers
    {
        public SurfaceModifiers(float traction, float brake, float rollingResistance, float lateralSpeedMultiplier)
        {
            Traction = traction;
            Brake = brake;
            RollingResistance = rollingResistance;
            LateralSpeedMultiplier = lateralSpeedMultiplier;
        }

        public float Traction { get; }
        public float Brake { get; }
        public float RollingResistance { get; }
        public float LateralSpeedMultiplier { get; }
    }
}
