using System;
using System.Collections.Generic;
using System.Linq;
using Autofac;
using SmartTeslaAmpSetter.Shared.Dtos.Settings;
using SmartTeslaAmpSetter.Shared.Enums;
using SmartTeslaAmpSetter.Shared.TimeProviding;
using Xunit;
using Xunit.Abstractions;
using CarState = SmartTeslaAmpSetter.Shared.Dtos.Settings.CarState;

namespace SmartTeslaAmpSetter.Tests.Services;

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
        var chargingService = Mock.Create<Server.Services.ChargingService>();
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
        var chargingService = Mock.Create<Server.Services.ChargingService>();
        var currentTimeProvider = Mock.Create<FakeDateTimeProvider>(
            new NamedParameter("dateTime", new DateTime(2022, 4, 1, 14, 0, 0)));
        var currentTime = currentTimeProvider.Now();

        var timeSpanToLatestTimeToReachMinSoc = TimeSpan.FromMinutes(60);

        var minSoc = 50;

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
        var chargingService = Mock.Create<Server.Services.ChargingService>();
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
        var geofence = "Home";
        var cars = new List<Car>()
        {
            new Car()
            {
                Id = 1,
                CarState = new CarState()
                {
                    Geofence = geofence,
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
                    Geofence = geofence,
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
        var chargingService = Mock.Create<Server.Services.ChargingService>();

        var relevantIds = chargingService.GetRelevantCarIds(geofence);

        Assert.Contains(1, relevantIds);
        Assert.Single(relevantIds);
    }

    [Fact]
    public void Gets_irrelevant_cars()
    {
        var geofence = "Home";
        var cars = new List<Car>()
        {
            new Car()
            {
                Id = 1,
                CarState = new CarState()
                {
                    Geofence = geofence,
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
                    Geofence = geofence,
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
        var chargingService = Mock.Create<Server.Services.ChargingService>();

        var irrelevantCars = chargingService.GetIrrelevantCars(chargingService.GetRelevantCarIds(geofence));
        
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
}