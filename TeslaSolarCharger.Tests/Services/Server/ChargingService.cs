using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Moq;
using TeslaSolarCharger.Server.Resources;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
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

    [Theory, MemberData(nameof(AutoFullSpeedChargeData))]
    public void Does_autoenable_fullspeed_charge_if_needed(DtoChargingSlot chargingSlot, bool shouldEnableFullSpeedCharge)
    {
        Mock.Mock<IDateTimeProvider>()
            .Setup(d => d.DateTimeOffSetNow())
            .Returns(new DateTimeOffset(2023, 2, 1, 10, 0, 0, TimeSpan.Zero));
        var car = new Car()
        {
            CarState = new CarState()
            {
                PlannedChargingSlots = new List<DtoChargingSlot>() { chargingSlot },
                AutoFullSpeedCharge = false,
            }
        };

        var chargingService = Mock.Create<TeslaSolarCharger.Server.Services.ChargingService>();

        chargingService.EnableFullSpeedChargeIfWithinPlannedChargingSlot(car);
        chargingService.DisableFullSpeedChargeIfWithinNonePlannedChargingSlot(car);
        Assert.Equal(car.CarState.AutoFullSpeedCharge, shouldEnableFullSpeedCharge);
        chargingService.EnableFullSpeedChargeIfWithinPlannedChargingSlot(car);
        Assert.Equal(car.CarState.AutoFullSpeedCharge, shouldEnableFullSpeedCharge);
    }

    [Theory, MemberData(nameof(AutoFullSpeedChargeData))]
    public void Does_autodisable_fullspeed_charge_if_needed(DtoChargingSlot chargingSlot, bool shouldEnableFullSpeedCharge)
    {
        Mock.Mock<IDateTimeProvider>()
            .Setup(d => d.DateTimeOffSetNow())
            .Returns(new DateTimeOffset(2023, 2, 1, 10, 0, 0, TimeSpan.Zero));
        var car = new Car()
        {
            CarState = new CarState()
            {
                PlannedChargingSlots = new List<DtoChargingSlot>() { chargingSlot },
                AutoFullSpeedCharge = true,
            }
        };

        var chargingService = Mock.Create<TeslaSolarCharger.Server.Services.ChargingService>();

        chargingService.EnableFullSpeedChargeIfWithinPlannedChargingSlot(car);
        chargingService.DisableFullSpeedChargeIfWithinNonePlannedChargingSlot(car);
        Assert.Equal(car.CarState.AutoFullSpeedCharge, shouldEnableFullSpeedCharge);
        chargingService.EnableFullSpeedChargeIfWithinPlannedChargingSlot(car);
        Assert.Equal(car.CarState.AutoFullSpeedCharge, shouldEnableFullSpeedCharge);
    }

    public static readonly object[][] AutoFullSpeedChargeData =
    {
        new object[] { new DtoChargingSlot() {ChargeStart = new DateTimeOffset(2023, 2, 1, 10, 0, 0, TimeSpan.Zero), ChargeEnd = new DateTimeOffset(2023, 2, 1, 11, 0, 0, TimeSpan.Zero) }, true },
        new object[] { new DtoChargingSlot() {ChargeStart = new DateTimeOffset(2023, 2, 1, 10, 0, 1, TimeSpan.Zero), ChargeEnd = new DateTimeOffset(2023, 2, 1, 11, 0, 0, TimeSpan.Zero) }, false },
        new object[] { new DtoChargingSlot() {ChargeStart = new DateTimeOffset(2023, 2, 1, 8, 0, 1, TimeSpan.Zero), ChargeEnd = new DateTimeOffset(2023, 2, 1, 9, 0, 0, TimeSpan.Zero) }, false },
    };

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

        chargingService.EnableFullSpeedChargeIfWithinPlannedChargingSlot(car);

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

        chargingService.DisableFullSpeedChargeIfWithinNonePlannedChargingSlot(car);

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

    [Fact]
    public void DoesSetShouldStartTimesCorrectly()
    {
        var car = new Car();
        var chargeTimeUpdateService = Mock.Create<TeslaSolarCharger.Server.Services.ChargingService>();
        var dateTime = new DateTime(2022, 12, 15, 10, 0, 0, DateTimeKind.Local);

        car.CarState.ShouldStopChargingSince = dateTime;
        car.CarState.EarliestSwitchOff = dateTime;

        Mock.Mock<IDateTimeProvider>().Setup(d => d.Now()).Returns(dateTime);
        var timeSpanUntilSwitchOn = TimeSpan.FromMinutes(5);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.TimespanUntilSwitchOn()).Returns(timeSpanUntilSwitchOn);
        chargeTimeUpdateService.SetEarliestSwitchOnToNowWhenNotAlreadySet(car);

        Assert.Equal(dateTime, car.CarState.ShouldStartChargingSince);
        Assert.Equal(dateTime + timeSpanUntilSwitchOn, car.CarState.EarliestSwitchOn);
    }

    [Fact]
    public void DoesSetShouldStopTimesCorrectly()
    {
        var car = new Car();
        var chargeTimeUpdateService = Mock.Create<TeslaSolarCharger.Server.Services.ChargingService>();
        var dateTime = new DateTime(2022, 12, 15, 10, 0, 0, DateTimeKind.Local);

        car.CarState.ShouldStartChargingSince = dateTime;
        car.CarState.EarliestSwitchOn = dateTime;

        Mock.Mock<IDateTimeProvider>().Setup(d => d.Now()).Returns(dateTime);
        var timeSpanUntilSwitchOn = TimeSpan.FromMinutes(5);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.TimespanUntilSwitchOff()).Returns(timeSpanUntilSwitchOn);
        chargeTimeUpdateService.SetEarliestSwitchOffToNowWhenNotAlreadySet(car);

        Assert.Equal(dateTime, car.CarState.ShouldStopChargingSince);
        Assert.Null(car.CarState.ShouldStartChargingSince);
        Assert.Equal(dateTime + timeSpanUntilSwitchOn, car.CarState.EarliestSwitchOff);
        Assert.Null(car.CarState.EarliestSwitchOn);
    }

    [Theory]
    [InlineData(ChargeMode.PvAndMinSoc)]
    [InlineData(ChargeMode.PvOnly)]
    public async Task Dont_Plan_Charging_If_Min_Soc_Reached(ChargeMode chargeMode)
    {
        var chargeDuration = TimeSpan.Zero;

        Mock.Mock<IChargeTimeCalculationService>()
            .Setup(c => c.CalculateTimeToReachMinSocAtFullSpeedCharge(It.IsAny<Car>()))
            .Returns(chargeDuration);

        var currentDate = DateTimeOffset.Now;

        var car = new Car
        {
            CarConfiguration = new CarConfiguration
            {
                ChargeMode = chargeMode,
                LatestTimeToReachSoC = currentDate.LocalDateTime,
            },
            CarState = new CarState(),
        };

        var chargingService = Mock.Create<TeslaSolarCharger.Server.Services.ChargingService>();
        var chargingSlots = await chargingService.PlanChargingSlots(car, currentDate).ConfigureAwait(false);

        Assert.Empty(chargingSlots);
    }

    [Fact]
    public void DoesConcatenateChargingSlotsCorrectly()
    {
        var chargingSlots = new List<DtoChargingSlot>()
        {
            new DtoChargingSlot()
            {
                ChargeStart = new DateTimeOffset(2022, 1, 10, 10, 0, 0, TimeSpan.Zero),
                ChargeEnd = new DateTimeOffset(2022, 1, 10, 11, 0, 0, TimeSpan.Zero),
            },
            new DtoChargingSlot()
            {
                ChargeStart = new DateTimeOffset(2022, 1, 10, 11, 0, 0, TimeSpan.Zero),
                ChargeEnd = new DateTimeOffset(2022, 1, 10, 12, 00, 0, TimeSpan.Zero),
            },
            new DtoChargingSlot()
            {
                ChargeStart = new DateTimeOffset(2022, 1, 10, 13, 0, 0, TimeSpan.Zero),
                ChargeEnd = new DateTimeOffset(2022, 1, 10, 13, 30, 0, TimeSpan.Zero),
            },
        };

        var combinedChargingTimeBeforeConcatenation = chargingSlots.Select(c => c.ChargeDuration.TotalHours).Sum();

        var chargingService = Mock.Create<TeslaSolarCharger.Server.Services.ChargingService>();
        var concatenatedChargingSlots = chargingService.ConcatenateChargeTimes(chargingSlots);


        Assert.Equal(combinedChargingTimeBeforeConcatenation, concatenatedChargingSlots.Select(c => c.ChargeDuration.TotalHours).Sum());
        Assert.Equal(2, concatenatedChargingSlots.Count);
    }

    [Theory, MemberData(nameof(CalculateCorrectChargeTimesWithoutStockPricesData))]
    public async Task Calculate_Correct_ChargeTimes_Without_Stock_Prices(ChargeMode chargeMode, DateTime latestTimeToReachSoc, DateTimeOffset currentDate, DateTimeOffset expectedStart)
    {
        var chargeDuration = TimeSpan.FromHours(1);

        Mock.Mock<IChargeTimeCalculationService>()
            .Setup(c => c.CalculateTimeToReachMinSocAtFullSpeedCharge(It.IsAny<Car>()))
            .Returns(chargeDuration);

        var car = new Car
        {
            CarConfiguration = new CarConfiguration
            {
                ChargeMode = chargeMode,
                LatestTimeToReachSoC = latestTimeToReachSoc,
            },
            CarState = new CarState(),
        };

        var chargingService = Mock.Create<TeslaSolarCharger.Server.Services.ChargingService>();
        var chargingSlots = await chargingService.PlanChargingSlots(car, currentDate).ConfigureAwait(false);

        Assert.Single(chargingSlots);

        var plannedChargingSlot = chargingSlots.First();


        var maximumErrorTime = TimeSpan.FromSeconds(1);
        var minimumStartTime = expectedStart - maximumErrorTime;
        var maximumStartTime = expectedStart + maximumErrorTime;
        Assert.InRange(plannedChargingSlot.ChargeStart, minimumStartTime, maximumStartTime);
        if (chargeMode == ChargeMode.MaxPower)
        {
            plannedChargingSlot.ChargeEnd = DateTimeOffset.MaxValue;
        }
        else
        {
            Assert.Equal(plannedChargingSlot.ChargeDuration, chargeDuration);
        }
        Assert.False(plannedChargingSlot.IsActive);
    }

    public static readonly object[][] CalculateCorrectChargeTimesWithoutStockPricesData =
    {
        new object[] { ChargeMode.MaxPower, new DateTime(2023, 2, 1, 0, 0, 0, DateTimeKind.Utc), new DateTimeOffset(2023, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2023, 2, 1, 0, 0, 0, TimeSpan.Zero) },
        new object[] { ChargeMode.PvAndMinSoc, new DateTime(2023, 2, 1, 0, 0, 0, DateTimeKind.Utc), new DateTimeOffset(2023, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2023, 2, 1, 0, 0, 0, TimeSpan.Zero) },
        new object[] { ChargeMode.PvOnly, new DateTime(2023, 2, 1, 3, 0, 0, DateTimeKind.Utc), new DateTimeOffset(2023, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2023, 2, 1, 2, 0, 0, TimeSpan.Zero) },
    };
}
