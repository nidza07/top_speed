using System;

namespace TopSpeed.Physics.Powertrain
{
    public static class LongitudinalStep
    {
        private const float TwoPi = (float)(Math.PI * 2.0);

        public static int ResolveThrust(int throttleInput, int brakeInput)
        {
            if (throttleInput == 0)
                return brakeInput;
            if (brakeInput == 0)
                return throttleInput;
            return -brakeInput > throttleInput ? brakeInput : throttleInput;
        }

        public static LongitudinalStepResult Compute(in LongitudinalStepInput input)
        {
            if (input.ElapsedSeconds <= 0f)
                return new LongitudinalStepResult(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);

            if (input.RequestDrive)
                return ComputeDrive(in input);

            return ComputeCoast(in input);
        }

        private static LongitudinalStepResult ComputeDrive(in LongitudinalStepInput input)
        {
            var throttle = Clamp(input.Throttle, 0f, 1f);
            var coupling = Clamp(input.DrivelineCouplingFactor, 0f, 1f);
            var driveScale = Math.Max(0f, input.DriveAccelerationScale);
            var accelMps2 = input.InReverse
                ? Calculator.ReverseAccel(
                    input.Config,
                    input.SpeedMps,
                    throttle,
                    input.SurfaceTractionModifier,
                    input.LongitudinalGripFactor,
                    input.SurfaceRollingResistanceModifier,
                    input.ResistanceEnvironment)
                : Calculator.DriveAccel(
                    input.Config,
                    input.Gear,
                    input.SpeedMps,
                    throttle,
                    input.SurfaceTractionModifier,
                    input.LongitudinalGripFactor,
                    input.SurfaceRollingResistanceModifier,
                    input.ResistanceEnvironment,
                    input.DriveRatioOverride);

            var resistance = ResistanceModel.Compute(
                input.Config,
                input.SpeedMps,
                input.SurfaceRollingResistanceModifier,
                applyDrivelineDrag: true,
                coupling,
                input.Gear,
                input.InReverse,
                input.IsNeutral,
                input.ResistanceEnvironment,
                input.DriveRatioOverride);
            var aerodynamicDecelKph = ForceToKphPerSecond(input.Config, resistance.AerodynamicForceN);
            var rollingResistanceDecelKph = ForceToKphPerSecond(input.Config, resistance.RollingResistanceForceN);
            var drivelineDragDecelKph = ForceToKphPerSecond(input.Config, resistance.DrivelineDragForceN);
            accelMps2 = (accelMps2 * coupling * driveScale) - ForceToMps2(input.Config, resistance.DrivelineDragForceN);

            var newSpeedMps = Math.Max(0f, input.SpeedMps + (accelMps2 * input.ElapsedSeconds));
            var speedDeltaKph = (newSpeedMps - input.SpeedMps) * 3.6f;
            var coupledDriveRpm = CoupledRpm(input.Config, input.Gear, newSpeedMps, input.InReverse, input.DriveRatioOverride);
            return new LongitudinalStepResult(
                speedDeltaKph,
                coupledDriveRpm,
                accelMps2,
                totalDecelKph: Math.Max(0f, aerodynamicDecelKph + rollingResistanceDecelKph + drivelineDragDecelKph),
                brakeDecelKph: 0f,
                engineBrakeDecelKph: 0f,
                aerodynamicDecelKph,
                rollingResistanceDecelKph,
                drivelineDragDecelKph);
        }

        private static LongitudinalStepResult ComputeCoast(in LongitudinalStepInput input)
        {
            var brakeInput = Clamp(input.Brake, 0f, 1f);
            var brakeDecel = input.RequestBrake
                ? Calculator.BrakeDecelKph(input.Config, brakeInput, input.SurfaceBrakeModifier)
                : 0f;
            var engineBrakeDecel = input.ApplyEngineBraking
                ? Calculator.EngineBrakeDecelKph(
                    input.Config,
                    input.Gear,
                    input.InReverse,
                    input.SpeedMps,
                    input.SurfaceBrakeModifier,
                    input.CurrentEngineRpm,
                    input.DriveRatioOverride)
                : 0f;
            var resistance = ResistanceModel.Compute(
                input.Config,
                input.SpeedMps,
                input.SurfaceRollingResistanceModifier,
                applyDrivelineDrag: true,
                input.DrivelineCouplingFactor,
                input.Gear,
                input.InReverse,
                input.IsNeutral,
                input.ResistanceEnvironment,
                input.DriveRatioOverride);
            var aerodynamicDecelKph = ForceToKphPerSecond(input.Config, resistance.AerodynamicForceN);
            var rollingResistanceDecelKph = ForceToKphPerSecond(input.Config, resistance.RollingResistanceForceN);
            var drivelineDragDecelKph = ForceToKphPerSecond(input.Config, resistance.DrivelineDragForceN);
            var totalDecelKph = aerodynamicDecelKph + rollingResistanceDecelKph + drivelineDragDecelKph + engineBrakeDecel + brakeDecel;
            var creepDeltaKph = Math.Max(0f, input.CreepAccelerationMps2) * input.ElapsedSeconds * 3.6f;
            var speedDeltaKph = (-totalDecelKph * input.ElapsedSeconds) + creepDeltaKph;
            return new LongitudinalStepResult(
                speedDeltaKph,
                coupledDriveRpm: 0f,
                driveAccelerationMps2: 0f,
                totalDecelKph,
                brakeDecelKph: brakeDecel,
                engineBrakeDecelKph: engineBrakeDecel,
                aerodynamicDecelKph,
                rollingResistanceDecelKph,
                drivelineDragDecelKph);
        }

        private static float CoupledRpm(
            Config config,
            int gear,
            float speedMps,
            bool inReverse,
            float? driveRatioOverride)
        {
            var wheelCircumference = config.WheelRadiusM * TwoPi;
            if (wheelCircumference <= 0f)
                return config.IdleRpm;

            var ratio = inReverse
                ? config.ReverseGearRatio
                : (driveRatioOverride.HasValue && driveRatioOverride.Value > 0f
                    ? driveRatioOverride.Value
                    : config.GetGearRatio(gear));
            var coupledRpm = (speedMps / wheelCircumference) * 60f * ratio * config.FinalDriveRatio;
            return Clamp(coupledRpm, config.IdleRpm, config.RevLimiter);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        private static float ForceToMps2(Config config, float forceN)
        {
            return config.MassKg > 0f ? forceN / config.MassKg : 0f;
        }

        private static float ForceToKphPerSecond(Config config, float forceN)
        {
            return ForceToMps2(config, forceN) * 3.6f;
        }
    }
}
