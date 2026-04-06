namespace TopSpeed.Physics.Powertrain
{
    public readonly struct ResistanceBreakdown
    {
        public ResistanceBreakdown(
            float aerodynamicForceN,
            float rollingResistanceForceN,
            float drivelineDragForceN)
        {
            AerodynamicForceN = aerodynamicForceN;
            RollingResistanceForceN = rollingResistanceForceN;
            DrivelineDragForceN = drivelineDragForceN;
        }

        public float AerodynamicForceN { get; }
        public float RollingResistanceForceN { get; }
        public float DrivelineDragForceN { get; }
        public float TotalForceN => AerodynamicForceN + RollingResistanceForceN + DrivelineDragForceN;
    }
}
