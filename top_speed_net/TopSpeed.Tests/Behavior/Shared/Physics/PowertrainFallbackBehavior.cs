using System;
using TopSpeed.Physics.Powertrain;
using TopSpeed.Physics.Torque;
using Xunit;

namespace TopSpeed.Tests;

[Trait("Category", "Behavior")]
public sealed class PowertrainFallbackBehaviorTests
{
    [Fact]
    public void BuildDefaults_ShouldFillExplicitResistanceValues()
    {
        var build = PowertrainBuild.Create(
            new BuildInput(
                massKg: 1450f,
                drivetrainEfficiency: 0.86f,
                engineBrakingTorqueNm: 240f,
                tireGripCoefficient: 0.92f,
                brakeStrength: 1.0f,
                wheelRadiusM: 0.32f,
                engineBraking: 0.28f,
                idleRpm: 800f,
                revLimiter: 6500f,
                finalDriveRatio: 4.0f,
                powerFactor: 0.75f,
                peakTorqueNm: 260f,
                peakTorqueRpm: 3200f,
                idleTorqueNm: 90f,
                redlineTorqueNm: 150f,
                dragCoefficient: 0.28f,
                frontalAreaM2: 2.10f,
                sideAreaM2: -1f,
                rollingResistanceCoefficient: 0.013f,
                rollingResistanceSpeedFactor: -1f,
                launchRpm: 2200f,
                reversePowerFactor: 0.55f,
                reverseGearRatio: 3.2f,
                reverseMaxSpeedKph: 35f,
                engineInertiaKgm2: 0.24f,
                engineFrictionTorqueNm: 20f,
                drivelineCouplingRate: 12f,
                gears: 6,
                torqueCurve: CurveFactory.FromLegacy(800f, 6500f, 3200f, 90f, 260f, 150f)));

        build.Powertrain.SideAreaM2.Should().BeApproximately(3.78f, 0.0001f);
        build.Powertrain.RollingResistanceSpeedFactor.Should().BeApproximately(0.01f, 0.0001f);
        build.CoupledDrivelineDragNm.Should().BeApproximately(18f, 0.0001f);
        build.CoupledDrivelineViscousDragNmPerKrpm.Should().BeApproximately(6f, 0.0001f);
    }

    [Fact]
    public void ExplicitResistanceDefaults_ShouldProduceReasonableCalmAirCoastdown()
    {
        var build = PowertrainBuild.Create(
            new BuildInput(
                massKg: 1450f,
                drivetrainEfficiency: 0.86f,
                engineBrakingTorqueNm: 240f,
                tireGripCoefficient: 0.92f,
                brakeStrength: 1.0f,
                wheelRadiusM: 0.32f,
                engineBraking: 0.28f,
                idleRpm: 800f,
                revLimiter: 6500f,
                finalDriveRatio: 4.0f,
                powerFactor: 0.75f,
                peakTorqueNm: 260f,
                peakTorqueRpm: 3200f,
                idleTorqueNm: 90f,
                redlineTorqueNm: 150f,
                dragCoefficient: 0.28f,
                frontalAreaM2: 2.10f,
                sideAreaM2: -1f,
                rollingResistanceCoefficient: 0.013f,
                rollingResistanceSpeedFactor: -1f,
                launchRpm: 2200f,
                reversePowerFactor: 0.55f,
                reverseGearRatio: 3.2f,
                reverseMaxSpeedKph: 35f,
                engineInertiaKgm2: 0.24f,
                engineFrictionTorqueNm: 20f,
                drivelineCouplingRate: 12f,
                gears: 6,
                torqueCurve: CurveFactory.FromLegacy(800f, 6500f, 3200f, 90f, 260f, 150f)));

        var speedKph = 100f;
        const float elapsed = 0.05f;
        const int steps = 160;

        for (var i = 0; i < steps; i++)
        {
            var speedMps = speedKph / 3.6f;
            var aerodynamic = Calculator.AerodynamicDecelKph(build.Powertrain, speedMps, ResistanceEnvironment.Calm);
            var rolling = Calculator.RollingResistanceDecelKph(build.Powertrain, speedMps, 1f);
            speedKph = Math.Max(0f, speedKph - ((aerodynamic + rolling) * elapsed));
        }

        speedKph.Should().BeInRange(82f, 91f);
    }
}
