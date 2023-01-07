using System;
using System.Collections.Generic;
using System.Linq;
using Autofac;
using TeslaSolarCharger.Server.Resources;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.TimeProviding;
using Xunit;
using Xunit.Abstractions;
using CarState = TeslaSolarCharger.Shared.Dtos.Settings.CarState;

namespace TeslaSolarCharger.Tests.Services.Server;

public class ChargingService : TestBase
{
    public ChargingService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Theory]
    [InlineData(ChargeMode.PvAndMinSoc, -32, 10, false)]
    [InlineData(ChargeMode.PvAndMinSoc, 2, -10, false)]
    [InlineData(ChargeMode.PvOnly, -32, 10, false)]
    [InlineData(ChargeMode.PvOnly, 2, -10, false)]
    [InlineData(ChargeMode.PvAndMinSoc, -32, 10, true)]
    [InlineData(ChargeMode.PvAndMinSoc, 2, -10, true)]
    [InlineData(ChargeMode.PvOnly, -32, 10, true)]
    [InlineData(ChargeMode.PvOnly, 2, -10, true)]
    public void Does_autoenable_fullspeed_charge_if_needed(ChargeMode chargeMode, int fullSpeedChargeMinutesAfterLatestTime, int moreSocThanMinSoc, bool autofullSpeedCharge)
    {
        var chargingService = Mock.Create<TeslaSolarCharger.Server.Services.ChargingService>();
        var currentTimeProvider = Mock.Create<FakeDateTimeProvider>(
            new NamedParameter("dateTime", new DateTime(2022, 4, 1, 14, 0, 0)));
        var currentTime = currentTimeProvider.Now();

        var timeSpanToLatestTimeToReachMinSoc = TimeSpan.FromMinutes(60);
        var timeSpanToReachMinSoCAtFullSpeedCharge = timeSpanToLatestTimeToReachMinSoc.Add(TimeSpan.FromMinutes(fullSpeedChargeMinutesAfterLatestTime));

        var minSoc = 50;

        var car = CreateDemoCar(chargeMode, currentTime + timeSpanToLatestTimeToReachMinSoc, minSoc + moreSocThanMinSoc, minSoc, autofullSpeedCharge);


        car.CarState.ReachingMinSocAtFullSpeedCharge = currentTime + timeSpanToReachMinSoCAtFullSpeedCharge;

        chargingService.EnableFullSpeedChargeIfMinimumSocNotReachable(car);
        chargingService.DisableFullSpeedChargeIfMinimumSocReachedOrMinimumSocReachable(car);


        if (fullSpeedChargeMinutesAfterLatestTime > 0)
        {
            Assert.True(car.CarState.AutoFullSpeedCharge);
            return;
        }

        if (moreSocThanMinSoc >= 0)
        {
            Assert.False(car.CarState.AutoFullSpeedCharge);
            return;
        }

        switch (chargeMode)
        {
            case ChargeMode.PvAndMinSoc:
                Assert.True(car.CarState.AutoFullSpeedCharge);
                break;

            case ChargeMode.PvOnly:
                Assert.False(car.CarState.AutoFullSpeedCharge);
                break;

            default:
                throw new NotImplementedException("This test does not handle this charge mode");
        }

    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Enable_Full_Speed_Charge_Can_Handle_Null_Values(bool autoFullSpeedCharge)
    {
        var chargingService = Mock.Create<TeslaSolarCharger.Server.Services.ChargingService>();
        var currentTimeProvider = Mock.Create<FakeDateTimeProvider>(
            new NamedParameter("dateTime", new DateTime(2022, 4, 1, 14, 0, 0)));
        var currentTime = currentTimeProvider.Now();

        var timeSpanToLatestTimeToReachMinSoc = TimeSpan.FromMinutes(60);

        var globalContants = Mock.Create<GlobalConstants>();
        var minSoc = globalContants.MinSocLimit;

        var car = CreateDemoCar(ChargeMode.PvAndMinSoc, currentTime + timeSpanToLatestTimeToReachMinSoc, minSoc + 10, minSoc, autoFullSpeedCharge);

        car.CarState.ReachingMinSocAtFullSpeedCharge = null;

        chargingService.EnableFullSpeedChargeIfMinimumSocNotReachable(car);

        Assert.Equal(autoFullSpeedCharge, car.CarState.AutoFullSpeedCharge);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Disable_Full_Speed_Charge_Can_Handle_Null_Values(bool autoFullSpeedCharge)
    {
        var chargingService = Mock.Create<TeslaSolarCharger.Server.Services.ChargingService>();
        var currentTimeProvider = Mock.Create<FakeDateTimeProvider>(
            new NamedParameter("dateTime", new DateTime(2022, 4, 1, 14, 0, 0)));
        var currentTime = currentTimeProvider.Now();

        var timeSpanToLatestTimeToReachMinSoc = TimeSpan.FromMinutes(60);

        var minSoc = 55;

        var car = CreateDemoCar(ChargeMode.PvOnly, currentTime + timeSpanToLatestTimeToReachMinSoc, minSoc - 10, minSoc, autoFullSpeedCharge);

        car.CarState.ReachingMinSocAtFullSpeedCharge = null;

        chargingService.DisableFullSpeedChargeIfMinimumSocReachedOrMinimumSocReachable(car);

        Assert.False(car.CarState.AutoFullSpeedCharge);
    }

    [Fact]
    public void Gets_relevant_car_IDs()
    {
        var cars = new List<Car>()
        {
            new Car()
            {
                Id = 1,
                CarState = new CarState()
                {
                    IsHomeGeofence = true,
                    PluggedIn = true,
                    ClimateOn = false,
                    ChargerActualCurrent = 3,
                    SoC = 30,
                    SocLimit = 60,
                },
                CarConfiguration = new CarConfiguration()
                {
                    ShouldBeManaged = true,
                },
            },
            new Car()
            {
                Id = 2,
                CarState = new CarState()
                {
                    Geofence = null,
                    PluggedIn = true,
                    ClimateOn = false,
                    ChargerActualCurrent = 3,
                    SoC = 30,
                    SocLimit = 60,
                },
                CarConfiguration = new CarConfiguration()
                {
                    ShouldBeManaged = true,
                },
            },
            new Car()
            {
                Id = 3,
                CarState = new CarState()
                {
                    IsHomeGeofence = true,
                    PluggedIn = true,
                    ClimateOn = false,
                    ChargerActualCurrent = 3,
                    SoC = 30,
                    SocLimit = 60,
                },
                CarConfiguration = new CarConfiguration()
                {
                    ShouldBeManaged = false,
                },
            },
        };
        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(cars);
        var chargingService = Mock.Create<TeslaSolarCharger.Server.Services.ChargingService>();

        var relevantIds = chargingService.GetRelevantCarIds();

        Assert.Contains(1, relevantIds);
        Assert.Single(relevantIds);
    }

    [Fact]
    public void Gets_irrelevant_cars()
    {
        var cars = new List<Car>()
        {
            new Car()
            {
                Id = 1,
                CarState = new CarState()
                {
                    IsHomeGeofence = true,
                    PluggedIn = true,
                    ClimateOn = false,
                    ChargerActualCurrent = 3,
                    SoC = 30,
                    SocLimit = 60,
                },
                CarConfiguration = new CarConfiguration() { ShouldBeManaged = true },
            },
            new Car()
            {
                Id = 2,
                CarState = new CarState()
                {
                    Geofence = null,
                    PluggedIn = true,
                    ClimateOn = false,
                    ChargerActualCurrent = 3,
                    SoC = 30,
                    SocLimit = 60,
                },
                CarConfiguration = new CarConfiguration() { ShouldBeManaged = true },
            },
            new Car()
            {
                Id = 3,
                CarState = new CarState()
                {
                    IsHomeGeofence = true,
                    PluggedIn = true,
                    ClimateOn = false,
                    ChargerActualCurrent = 3,
                    SoC = 30,
                    SocLimit = 60,
                },
                CarConfiguration = new CarConfiguration() { ShouldBeManaged = false },
            },
        };
        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(cars);
        var chargingService = Mock.Create<TeslaSolarCharger.Server.Services.ChargingService>();

        var irrelevantCars = chargingService.GetIrrelevantCars(chargingService.GetRelevantCarIds());
        
        Assert.Equal(2, irrelevantCars.Count);
        Assert.Contains(2, irrelevantCars.Select(c => c.Id));
        Assert.Contains(3, irrelevantCars.Select(c => c.Id));
    }
    
    private Car CreateDemoCar(ChargeMode chargeMode, DateTime latestTimeToReachSoC, int soC, int minimumSoC, bool autoFullSpeedCharge)
    {
        var car = new Car()
        {
            CarState = new CarState()
            {
                AutoFullSpeedCharge = autoFullSpeedCharge,
                SoC = soC,
            },
            CarConfiguration = new CarConfiguration()
            {
                LatestTimeToReachSoC = latestTimeToReachSoC,
                MinimumSoC = minimumSoC,
                ChargeMode = chargeMode,
            },
        };
        return car;
    }

    [Theory]
    [InlineData(3)]
    [InlineData(1)]
    public void Calculates_Correct_Charge_MaxSpeed_Charge_Time(int numberOfPhases)
    {
        var car = new Car()
        {
            Id = 1,
            CarState = new CarState()
            {
                PluggedIn = true,
                SoC = 30,
                ChargerPhases = numberOfPhases
            },
            CarConfiguration = new CarConfiguration()
            {
                MinimumSoC = 45,
                UsableEnergy = 74,
                MaximumAmpere = 16,
            }
        };


        var dateTime = new DateTime(2022, 4, 1, 14, 0, 0);
        Mock.Mock<IDateTimeProvider>().Setup(d => d.Now()).Returns(dateTime);
        var chargingService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimeUpdateService>();

        chargingService.UpdateChargeTime(car);

        var lowerMinutes = 60 * (3 / numberOfPhases);

#pragma warning disable CS8629
        Assert.InRange((DateTime)car.CarState.ReachingMinSocAtFullSpeedCharge, dateTime.AddMinutes(lowerMinutes), dateTime.AddMinutes(lowerMinutes + 1));
#pragma warning restore CS8629
    }

    [Fact]
    public void Handles_Plugged_Out_Car()
    {
        var car = new Car()
        {
            Id = 1,
            CarState = new CarState()
            {
                PluggedIn = false,
                SoC = 30,
                ChargerPhases = 1
            },
            CarConfiguration = new CarConfiguration()
            {
                MinimumSoC = 45,
                UsableEnergy = 74,
                MaximumAmpere = 16,
            }
        };


        var dateTime = new DateTime(2022, 4, 1, 14, 0, 0);
        Mock.Mock<IDateTimeProvider>().Setup(d => d.Now()).Returns(dateTime);
        var chargingService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimeUpdateService>();

        chargingService.UpdateChargeTime(car);

        Assert.Null(car.CarState.ReachingMinSocAtFullSpeedCharge);
    }

    [Fact]
    public void Handles_Reaced_Minimum_Soc()
    {
        var car = new Car()
        {
            Id = 1,
            CarState = new CarState()
            {
                PluggedIn = true,
                SoC = 30,
                ChargerPhases = 1
            },
            CarConfiguration = new CarConfiguration()
            {
                MinimumSoC = 30,
                UsableEnergy = 74,
                MaximumAmpere = 16,
            }
        };


        var dateTime = new DateTime(2022, 4, 1, 14, 0, 0);
        Mock.Mock<IDateTimeProvider>().Setup(d => d.Now()).Returns(dateTime);
        var chargingService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimeUpdateService>();

        chargingService.UpdateChargeTime(car);

        Assert.Equal(dateTime, car.CarState.ReachingMinSocAtFullSpeedCharge);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(null, null, null, null, 0, 0)]
    [InlineData(null, null, null, null, 10, 10)]
    [InlineData(null, null, null, null, -10, -10)]
    [InlineData(10, 100, 20, 200, 0, -100)]
    [InlineData(30, 100, 20, 200, 0, 100)]
    [InlineData(10, 200, 20, 200, 0, 0)]
    public void AddsCorrectChargingPowerBasedOnHomeBatteryState(int? homeBatterySoc, int? homeBatteryPower, int? homeBatteryMinSoc,
        int? homeBatteryMinChargingPower, int overage, int expectedOverage)
    {
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryMinSoc()).Returns(homeBatteryMinSoc);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryChargingPower()).Returns(homeBatteryMinChargingPower);
        Mock.Mock<ISettings>().Setup(c => c.HomeBatterySoc).Returns(homeBatterySoc);
        Mock.Mock<ISettings>().Setup(c => c.HomeBatteryPower).Returns(homeBatteryPower);


        var chargingService = Mock.Create<TeslaSolarCharger.Server.Services.ChargingService>();

        var newOverage = chargingService.AddHomeBatteryStateToPowerCalculation(overage);

        Assert.Equal(expectedOverage, newOverage);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(0, null, 1000, 0)]
    [InlineData(10, 20, 1000, 1000)]
    [InlineData(10, 20, null, 0)]
    [InlineData(10, null, 1000, 0)]
    [InlineData(20, 20, 1000, 0)]
    [InlineData(30, 20, 1000, 0)]
    public void GetsCorrectTargetBatteryChargingPower(int? actualHomeBatterySoc, int? homeBatteryMinSoc, int? homeBatteryMaxChargingPower,
        int expectedTargetChargingPower)
    {
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryMinSoc()).Returns(homeBatteryMinSoc);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryChargingPower()).Returns(homeBatteryMaxChargingPower);
        Mock.Mock<ISettings>().Setup(c => c.HomeBatterySoc).Returns(actualHomeBatterySoc);

        var chargingService = Mock.Create<TeslaSolarCharger.Server.Services.ChargingService>();

        var targetChargingPower =
            chargingService.GetBatteryTargetChargingPower();

        Assert.Equal(expectedTargetChargingPower, targetChargingPower);
    }
}
