using System;
using FsCheck.Fluent;
using FsCheck.Xunit;
using TopSpeed.Physics.Powertrain;
using TopSpeed.Physics.Torque;
using Xunit;

namespace TopSpeed.Tests
{
    [Trait("Category", "Invariant")]
    public sealed class PowertrainInvariantTests
    {
        [Property(MaxTest = 100, Arbitrary = new[] { typeof(PowertrainArbitraries) })]
        public void CalmAirPassiveResistance_ShouldBeFiniteAndNonNegative(PowertrainScenario scenario)
        {
            var config = scenario.BuildConfig();
            var aerodynamic = Calculator.AerodynamicDecelKph(config, scenario.SpeedMps, ResistanceEnvironment.Calm);
            var rolling = Calculator.RollingResistanceDecelKph(config, scenario.SpeedMps, 1f);

            (!float.IsNaN(aerodynamic) && !float.IsInfinity(aerodynamic)).Should().BeTrue();
            (!float.IsNaN(rolling) && !float.IsInfinity(rolling)).Should().BeTrue();
            aerodynamic.Should().BeGreaterThanOrEqualTo(0f);
            rolling.Should().BeGreaterThanOrEqualTo(0f);
        }

        [Property(MaxTest = 100, Arbitrary = new[] { typeof(PowertrainArbitraries) })]
        public void NeutralCoast_ShouldNeverIncreaseSpeed(PowertrainScenario scenario)
        {
            var config = scenario.BuildConfig();
            var speedKph = scenario.SpeedMps * 3.6f;

            for (var i = 0; i < scenario.Steps; i++)
            {
                var speedMps = speedKph / 3.6f;
                var aerodynamic = Calculator.AerodynamicDecelKph(config, speedMps, ResistanceEnvironment.Calm);
                var rolling = Calculator.RollingResistanceDecelKph(config, speedMps, 1f);
                speedKph = Math.Max(0f, speedKph - ((aerodynamic + rolling) * scenario.ElapsedSeconds));
            }

            (!float.IsNaN(speedKph) && !float.IsInfinity(speedKph)).Should().BeTrue();
            speedKph.Should().BeLessThanOrEqualTo(scenario.SpeedMps * 3.6f + 0.001f);
            speedKph.Should().BeGreaterThanOrEqualTo(0f);
        }

        [Property(MaxTest = 100, Arbitrary = new[] { typeof(PowertrainArbitraries) })]
        public void ZeroGripDriveAccel_ShouldNeverBePositive(PowertrainScenario scenario)
        {
            var config = scenario.BuildConfig();
            var accel = Calculator.DriveAccel(
                config,
                gear: Math.Min(scenario.Gear, config.Gears),
                speedMps: scenario.SpeedMps,
                throttle: scenario.Throttle,
                surfaceTractionModifier: 1f,
                longitudinalGripFactor: 0f,
                rollingResistanceModifier: 1f,
                resistanceEnvironment: ResistanceEnvironment.Calm);

            (!float.IsNaN(accel) && !float.IsInfinity(accel)).Should().BeTrue();
            accel.Should().BeLessThanOrEqualTo(0f);
        }
    }

    public sealed record PowertrainScenario(
        float MassKg,
        float DragCoefficient,
        float FrontalAreaM2,
        float SideAreaM2,
        float RollingResistanceCoefficient,
        float RollingResistanceSpeedFactor,
        float CoupledDrivelineDragNm,
        float CoupledDrivelineViscousDragNmPerKrpm,
        float SpeedMps,
        float Throttle,
        int Gear,
        int Steps,
        float ElapsedSeconds)
    {
        public TopSpeed.Physics.Powertrain.Config BuildConfig()
        {
            var torqueCurve = CurveFactory.FromLegacy(800f, 6500f, 3200f, 90f, 260f, 150f);
            return new TopSpeed.Physics.Powertrain.Config(
                MassKg,
                drivetrainEfficiency: 0.86f,
                engineBrakingTorqueNm: 220f,
                tireGripCoefficient: 0.92f,
                brakeStrength: 1.0f,
                wheelRadiusM: 0.31f,
                engineBraking: 0.30f,
                idleRpm: 800f,
                revLimiter: 6500f,
                finalDriveRatio: 4.0f,
                powerFactor: 0.75f,
                peakTorqueNm: 260f,
                peakTorqueRpm: 3200f,
                idleTorqueNm: 90f,
                redlineTorqueNm: 150f,
                dragCoefficient: DragCoefficient,
                frontalAreaM2: FrontalAreaM2,
                sideAreaM2: SideAreaM2,
                rollingResistanceCoefficient: RollingResistanceCoefficient,
                rollingResistanceSpeedFactor: RollingResistanceSpeedFactor,
                launchRpm: 2200f,
                reversePowerFactor: 0.55f,
                reverseGearRatio: 3.2f,
                engineInertiaKgm2: 0.24f,
                engineFrictionTorqueNm: 20f,
                drivelineCouplingRate: 12f,
                gears: 6,
                gearRatios: new[] { 3.60f, 2.10f, 1.50f, 1.15f, 0.95f, 0.82f },
                torqueCurve: torqueCurve,
                coupledDrivelineDragNm: CoupledDrivelineDragNm,
                coupledDrivelineViscousDragNmPerKrpm: CoupledDrivelineViscousDragNmPerKrpm);
        }
    }

    public static class PowertrainArbitraries
    {
        public static FsCheck.Arbitrary<PowertrainScenario> PowertrainScenario()
        {
            var generator =
                from mass in Gen.Choose(700, 2600)
                from drag in Gen.Choose(22, 42)
                from area in Gen.Choose(16, 30)
                from sideArea in Gen.Choose(30, 65)
                from rr in Gen.Choose(10, 18)
                from rrSpeedFactor in Gen.Choose(0, 30)
                from drivelineDrag in Gen.Choose(5, 40)
                from drivelineViscous in Gen.Choose(0, 20)
                from speed in Gen.Choose(0, 80)
                from throttle in Gen.Choose(0, 100)
                from gear in Gen.Choose(1, 6)
                from steps in Gen.Choose(1, 200)
                from elapsed in Gen.Choose(1, 10)
                select new PowertrainScenario(
                    MassKg: mass,
                    DragCoefficient: drag / 100f,
                    FrontalAreaM2: area / 10f,
                    SideAreaM2: sideArea / 10f,
                    RollingResistanceCoefficient: rr / 1000f,
                    RollingResistanceSpeedFactor: rrSpeedFactor / 1000f,
                    CoupledDrivelineDragNm: drivelineDrag,
                    CoupledDrivelineViscousDragNmPerKrpm: drivelineViscous,
                    SpeedMps: speed,
                    Throttle: throttle / 100f,
                    Gear: gear,
                    Steps: steps,
                    ElapsedSeconds: elapsed / 100f);

            return Arb.From(generator);
        }
    }
}
