using System.Linq;
using TopSpeed.Protocol;
using TopSpeed.Vehicles;
using Xunit;

namespace TopSpeed.Tests
{
    [Trait("Category", "Behavior")]
    public sealed class VehicleCatalogBehaviorTests
    {
        [Theory]
        [InlineData(CarType.Vehicle10)]
        [InlineData(CarType.Vehicle11)]
        [InlineData(CarType.Vehicle12)]
        public void Motorcycles_ShouldUseTightUpperGears_AndReasonableCoastDrag(CarType carType)
        {
            var spec = OfficialVehicleCatalog.Get((int)carType);
            var fifthTop = PowertrainHarness.GearTopSpeedKph(spec, 5);
            var sixthTop = PowertrainHarness.GearTopSpeedKph(spec, 6);

            spec.GearRatios.Length.Should().Be(6);
            spec.GearRatios.Should().BeInDescendingOrder();
            sixthTop.Should().BeInRange(spec.TopSpeed * 0.95f, spec.TopSpeed * 1.10f);
            sixthTop.Should().BeLessThanOrEqualTo(fifthTop * 1.15f);
            spec.DragCoefficient.Should().BePositive();
            spec.FrontalAreaM2.Should().BePositive();
            (spec.SideAreaM2 > 0f ? spec.SideAreaM2 : spec.FrontalAreaM2 * 1.8f).Should().BeGreaterThan(spec.FrontalAreaM2);
            spec.RollingResistanceCoefficient.Should().BePositive();
            spec.RollingResistanceSpeedFactor.Should().BeGreaterThan(0.011f);
            spec.CoupledDrivelineDragNm.Should().BeGreaterThan(2.5f);
            spec.CoupledDrivelineViscousDragNmPerKrpm.Should().BeGreaterThan(2f);
        }

        [Theory]
        [InlineData(CarType.Vehicle6)]
        [InlineData(CarType.Vehicle8)]
        [InlineData(CarType.Vehicle9)]
        public void AutomaticFamily_ShouldUseUsefulTopGears_AndExplicitCoastDrag(CarType carType)
        {
            var spec = OfficialVehicleCatalog.Get((int)carType);
            var top = PowertrainHarness.GearTopSpeedKph(spec, spec.GearRatios.Length);
            var previous = PowertrainHarness.GearTopSpeedKph(spec, spec.GearRatios.Length - 1);

            top.Should().BeGreaterThan(previous);
            top.Should().BeLessThanOrEqualTo(previous * 1.22f);
            top.Should().BeInRange(spec.TopSpeed * 1.00f, spec.TopSpeed * 1.12f);
            spec.DragCoefficient.Should().BePositive();
            spec.FrontalAreaM2.Should().BePositive();
            (spec.SideAreaM2 > 0f ? spec.SideAreaM2 : spec.FrontalAreaM2 * 1.8f).Should().BeGreaterThan(spec.FrontalAreaM2);
            spec.RollingResistanceCoefficient.Should().BePositive();
            spec.RollingResistanceSpeedFactor.Should().BeGreaterThan(0.01f);
            spec.CoupledDrivelineDragNm.Should().BeGreaterThan(14f);
            spec.CoupledDrivelineViscousDragNmPerKrpm.Should().BeGreaterThan(5f);
        }

        [Theory]
        [InlineData(CarType.Vehicle1)]
        [InlineData(CarType.Vehicle2)]
        [InlineData(CarType.Vehicle7)]
        public void PerformanceFamily_ShouldUsePullingTopGears_AndExplicitCoastDrag(CarType carType)
        {
            var spec = OfficialVehicleCatalog.Get((int)carType);
            var top = PowertrainHarness.GearTopSpeedKph(spec, spec.GearRatios.Length);
            var previous = PowertrainHarness.GearTopSpeedKph(spec, spec.GearRatios.Length - 1);

            top.Should().BeGreaterThan(previous);
            top.Should().BeLessThanOrEqualTo(previous * 1.20f);
            top.Should().BeInRange(spec.TopSpeed * 0.98f, spec.TopSpeed * 1.08f);
            spec.DragCoefficient.Should().BePositive();
            spec.FrontalAreaM2.Should().BePositive();
            (spec.SideAreaM2 > 0f ? spec.SideAreaM2 : spec.FrontalAreaM2 * 1.8f).Should().BeGreaterThan(spec.FrontalAreaM2);
            spec.RollingResistanceCoefficient.Should().BePositive();
            spec.RollingResistanceSpeedFactor.Should().BeGreaterThan(0.01f);
            spec.CoupledDrivelineDragNm.Should().BeGreaterThan(15f);
            spec.CoupledDrivelineViscousDragNmPerKrpm.Should().BeGreaterThan(5.3f);
        }

        [Theory]
        [InlineData(CarType.Vehicle3)]
        [InlineData(CarType.Vehicle4)]
        [InlineData(CarType.Vehicle5)]
        public void ManualFamily_ShouldUseReasonableTopGears_AndExplicitCoastDrag(CarType carType)
        {
            var spec = OfficialVehicleCatalog.Get((int)carType);
            var top = PowertrainHarness.GearTopSpeedKph(spec, spec.GearRatios.Length);
            var previous = PowertrainHarness.GearTopSpeedKph(spec, spec.GearRatios.Length - 1);

            top.Should().BeGreaterThan(previous);
            top.Should().BeLessThanOrEqualTo(previous * 1.26f);
            top.Should().BeInRange(spec.TopSpeed * 0.98f, spec.TopSpeed * 1.10f);
            spec.DragCoefficient.Should().BePositive();
            spec.FrontalAreaM2.Should().BePositive();
            (spec.SideAreaM2 > 0f ? spec.SideAreaM2 : spec.FrontalAreaM2 * 1.8f).Should().BeGreaterThan(spec.FrontalAreaM2);
            spec.RollingResistanceCoefficient.Should().BePositive();
            spec.RollingResistanceSpeedFactor.Should().BeGreaterThan(0.01f);
            spec.CoupledDrivelineDragNm.Should().BeGreaterThan(14f);
            spec.CoupledDrivelineViscousDragNmPerKrpm.Should().BeGreaterThan(5f);
        }

        [Theory]
        [InlineData(CarType.Vehicle1, 90.9f, 91.6f)]
        [InlineData(CarType.Vehicle2, 89.6f, 90.3f)]
        [InlineData(CarType.Vehicle3, 86.8f, 87.4f)]
        [InlineData(CarType.Vehicle4, 88.9f, 89.5f)]
        [InlineData(CarType.Vehicle5, 86.1f, 86.7f)]
        [InlineData(CarType.Vehicle6, 89.6f, 90.6f)]
        [InlineData(CarType.Vehicle7, 89.8f, 90.5f)]
        [InlineData(CarType.Vehicle8, 90.0f, 91.0f)]
        [InlineData(CarType.Vehicle9, 84.8f, 85.5f)]
        [InlineData(CarType.Vehicle10, 81.0f, 81.7f)]
        [InlineData(CarType.Vehicle11, 81.5f, 82.3f)]
        [InlineData(CarType.Vehicle12, 81.5f, 82.3f)]
        public void NeutralCoast_ShouldStayWithinReasonableFamilyRange(CarType carType, float minFinalSpeedKph, float maxFinalSpeedKph)
        {
            var spec = OfficialVehicleCatalog.Get((int)carType);
            var trace = PowertrainHarness.SimulateNeutralCoast(spec);

            trace.FinalSpeedKph.Should().BeInRange(minFinalSpeedKph, maxFinalSpeedKph);
        }

        [Fact]
        public void Motorcycles_ShouldCoastDownFasterThanRepresentativeCars()
        {
            var motorcycleAverage = new[]
            {
                CarType.Vehicle10,
                CarType.Vehicle11,
                CarType.Vehicle12
            }
            .Select(type => PowertrainHarness.SimulateNeutralCoast(OfficialVehicleCatalog.Get((int)type)).FinalSpeedKph)
            .Average();

            var carAverage = new[]
            {
                CarType.Vehicle1,
                CarType.Vehicle4,
                CarType.Vehicle8
            }
            .Select(type => PowertrainHarness.SimulateNeutralCoast(OfficialVehicleCatalog.Get((int)type)).FinalSpeedKph)
            .Average();

            motorcycleAverage.Should().BeLessThan(carAverage - 8f);
        }

        [Fact]
        public void HeavyAutomaticVehicle_ShouldCoastDownFasterThanRepresentativeAutomaticCars()
        {
            var carAverage = new[]
            {
                CarType.Vehicle6,
                CarType.Vehicle8
            }
            .Select(type => PowertrainHarness.SimulateNeutralCoast(OfficialVehicleCatalog.Get((int)type)).FinalSpeedKph)
            .Average();

            var sprinterCoast = PowertrainHarness
                .SimulateNeutralCoast(OfficialVehicleCatalog.Get((int)CarType.Vehicle9))
                .FinalSpeedKph;

            sprinterCoast.Should().BeLessThan(carAverage - 2f);
        }

        [Fact]
        public void Porsche911Gt3Rs_ShouldHaveUsableTopGearPull()
        {
            var pull = PowertrainHarness.SimulateTopGearPull(OfficialVehicleCatalog.Get((int)CarType.Vehicle2));

            pull.DeltaKph.Should().BeGreaterThan(20f);
        }

        [Fact]
        public void Sprinter_ShouldLoseMoreNormalizedHighSpeedCoastThanCamryAndBmw()
        {
            var camry = PowertrainHarness.SimulateHighSpeedNeutralCoast(OfficialVehicleCatalog.Get((int)CarType.Vehicle6));
            var bmw = PowertrainHarness.SimulateHighSpeedNeutralCoast(OfficialVehicleCatalog.Get((int)CarType.Vehicle8));
            var sprinter = PowertrainHarness.SimulateHighSpeedNeutralCoast(OfficialVehicleCatalog.Get((int)CarType.Vehicle9));
            var carAverageLossFraction = (camry.LossFraction + bmw.LossFraction) / 2f;

            sprinter.LossFraction.Should().BeGreaterThan(carAverageLossFraction + 0.03f);
        }

        [Theory]
        [InlineData(CarType.Vehicle10)]
        [InlineData(CarType.Vehicle11)]
        [InlineData(CarType.Vehicle12)]
        public void Motorcycles_ShouldNoLongerHaveDeadTopGears(CarType carType)
        {
            var pull = PowertrainHarness.SimulateTopGearPull(OfficialVehicleCatalog.Get((int)carType));

            pull.DeltaKph.Should().BeGreaterThan(45f);
        }

        [Theory]
        [InlineData(CarType.Vehicle10)]
        [InlineData(CarType.Vehicle11)]
        [InlineData(CarType.Vehicle12)]
        public void Motorcycles_ShouldKeepClosedThrottleDecelBelowBrakeLikeLevels(CarType carType)
        {
            var decel = PowertrainHarness.SimulateHighSpeedClosedThrottle(OfficialVehicleCatalog.Get((int)carType));

            decel.FinalSpeedKph.Should().BeGreaterThan(70f);
            decel.LossFraction.Should().BeLessThan(0.60f);
        }

        [Theory]
        [InlineData(CarType.Vehicle10)]
        [InlineData(CarType.Vehicle11)]
        [InlineData(CarType.Vehicle12)]
        public void Motorcycles_ShouldFreeRevQuicklyWhenDisengaged(CarType carType)
        {
            var trace = EngineHarness.SimulateOfficialDisengagedFreeRev(OfficialVehicleCatalog.Get((int)carType));

            trace.FinalRpm.Should().BeGreaterThan(11000f);
        }

        [Fact]
        public void Motorcycles_ShouldFreeRevMuchFasterThanRepresentativeCars()
        {
            var motorcycleAverage = new[]
            {
                CarType.Vehicle10,
                CarType.Vehicle11,
                CarType.Vehicle12
            }
            .Select(type => EngineHarness.SimulateOfficialDisengagedFreeRev(OfficialVehicleCatalog.Get((int)type)).FinalRpm)
            .Average();

            var carAverage = new[]
            {
                CarType.Vehicle1,
                CarType.Vehicle4,
                CarType.Vehicle8
            }
            .Select(type => EngineHarness.SimulateOfficialDisengagedFreeRev(OfficialVehicleCatalog.Get((int)type)).FinalRpm)
            .Average();

            motorcycleAverage.Should().BeGreaterThan(carAverage + 3000f);
        }

        [Theory]
        [InlineData(CarType.Vehicle10)]
        [InlineData(CarType.Vehicle11)]
        [InlineData(CarType.Vehicle12)]
        public void Motorcycles_ShouldDropRpmQuicklyAfterFreeRevLift(CarType carType)
        {
            var trace = EngineHarness.SimulateOfficialDisengagedFreeRevLift(OfficialVehicleCatalog.Get((int)carType));

            trace.RpmAtLift.Should().BeGreaterThan(12000f);
            trace.FinalRpm.Should().BeLessThan(9000f);
        }
    }
}
