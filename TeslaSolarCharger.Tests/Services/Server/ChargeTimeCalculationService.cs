using Moq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using Xunit;
using Xunit.Abstractions;
using DateTime = System.DateTime;

namespace TeslaSolarCharger.Tests.Services.Server;

public class ChargeTimeCalculationService : TestBase
{
    public ChargeTimeCalculationService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Theory]
    [InlineData(0, 30, 75, 3, 16, 0, CarStateEnum.Charging)]
    [InlineData(31, 30, 100, 3, 16, 326, CarStateEnum.Charging)]
    [InlineData(31, 30, 100, 3, 16, 0, CarStateEnum.Asleep)]
    [InlineData(32, 30, 100, 3, 32, 326, CarStateEnum.Asleep)]
    [InlineData(32, 30, 50, 3, 16, 326, CarStateEnum.Asleep)]
    [InlineData(42, 40, 50, 3, 16, 326, CarStateEnum.Asleep)]
    [InlineData(42, 40, 50, 1, 16, 978, CarStateEnum.Asleep)]
    [InlineData(47, 40, 50000, 1, 15215, 3600, CarStateEnum.Asleep)]
    [InlineData(47, 40, 50000, 1, 15215, 3600, CarStateEnum.Charging)]
    public void Calculates_Correct_Full_Speed_Charge_Durations(int minimumSoc, int? acutalSoc, int usableEnergy,
        int chargerPhases, int maximumAmpere, double expectedTotalSeconds, CarStateEnum carState)
    {
        var car = new DtoCar()
        {
            MinimumSoC = minimumSoc,
            UsableEnergy = usableEnergy,
            MaximumAmpere = maximumAmpere,
            SoC = acutalSoc,
            ChargerPhases = chargerPhases,
            State = carState,
        };

        var chargeTimeCalculationService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimeCalculationService>();
        var chargeDuration = chargeTimeCalculationService.CalculateTimeToReachMinSocAtFullSpeedCharge(car);

        var expectedTimeSpan = TimeSpan.FromSeconds(expectedTotalSeconds);
        var maximumErrorTime = TimeSpan.FromSeconds(1);
        var minimum = expectedTimeSpan - maximumErrorTime;
        var maximum = expectedTimeSpan + maximumErrorTime;
        Assert.InRange(chargeDuration, minimum, maximum);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(1)]
    public void Calculates_Correct_Charge_MaxSpeed_Charge_Time(int numberOfPhases)
    {
        var car = new DtoCar()
        {
            Id = 1,
            PluggedIn = true,
            SoC = 30,
            ChargerPhases = numberOfPhases,
            MinimumSoC = 45,
            UsableEnergy = 74,
            MaximumAmpere = 16,
        };


        var dateTime = new DateTime(2022, 4, 1, 14, 0, 0);
        Mock.Mock<IDateTimeProvider>().Setup(d => d.Now()).Returns(dateTime);
        var chargeTimeCalculationService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimeCalculationService>();

        chargeTimeCalculationService.UpdateChargeTime(car);

        var lowerMinutes = 60 * (3 / numberOfPhases);

#pragma warning disable CS8629
        Assert.InRange((DateTime)car.ReachingMinSocAtFullSpeedCharge, dateTime.AddMinutes(lowerMinutes), dateTime.AddMinutes(lowerMinutes + 1));
#pragma warning restore CS8629
    }

    [Fact]
    public void Handles_Reaced_Minimum_Soc()
    {
        var car = new DtoCar()
        {
            Id = 1,
            PluggedIn = true,
            SoC = 30,
            ChargerPhases = 1,
            MinimumSoC = 30,
            UsableEnergy = 74,
            MaximumAmpere = 16,
        };


        var dateTime = new DateTime(2022, 4, 1, 14, 0, 0);
        Mock.Mock<IDateTimeProvider>().Setup(d => d.Now()).Returns(dateTime);
        var chargeTimeCalculationService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimeCalculationService>();

        chargeTimeCalculationService.UpdateChargeTime(car);

        Assert.Equal(dateTime, car.ReachingMinSocAtFullSpeedCharge);
    }

    [Theory]
    [InlineData(ChargeMode.PvAndMinSoc)]
    [InlineData(ChargeMode.PvOnly)]
    public async Task Dont_Plan_Charging_If_Min_Soc_Reached(ChargeMode chargeMode)
    {
        var chargeDuration = TimeSpan.Zero;

        Mock.Mock<IChargeTimeCalculationService>()
            .Setup(c => c.CalculateTimeToReachMinSocAtFullSpeedCharge(It.IsAny<DtoCar>()))
            .Returns(chargeDuration);

        var currentDate = DateTimeOffset.Now;

        var car = new DtoCar
        {
                ChargeMode = chargeMode,
                LatestTimeToReachSoC = currentDate.LocalDateTime,
        };

        var chargeTimeCalculationService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimeCalculationService>();
        // ReSharper disable once UseConfigureAwaitFalse
        var chargingSlots = await chargeTimeCalculationService.PlanChargingSlots(car, currentDate);

        Assert.Empty(chargingSlots);
    }

    [Fact]
    public void Does_Concatenate_Charging_Slots_Correctly_One_Partial_Slot_Only()
    {
        var chargingSlots = new List<DtoChargingSlot>()
        {
            new DtoChargingSlot()
            {
                ChargeStart = new DateTimeOffset(2022, 1, 10, 10, 0, 0, TimeSpan.Zero),
                ChargeEnd = new DateTimeOffset(2022, 1, 10, 10, 30, 0, TimeSpan.Zero),
            },
        };

        var combinedChargingTimeBeforeConcatenation = chargingSlots.Select(c => c.ChargeDuration.TotalHours).Sum();

        var chargeTimeCalculationService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimeCalculationService>();
        var concatenatedChargingSlots = chargeTimeCalculationService.ConcatenateChargeTimes(chargingSlots);


        Assert.Equal(combinedChargingTimeBeforeConcatenation, concatenatedChargingSlots.Select(c => c.ChargeDuration.TotalHours).Sum());
        Assert.Single(concatenatedChargingSlots);
    }

    [Fact]
    public void Does_Concatenate_Charging_Slots_Correctly_Two_Full_Hour_One_Partial_Hour_Last()
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
                ChargeEnd = new DateTimeOffset(2022, 1, 10, 12, 0, 0, TimeSpan.Zero),
            },
            new DtoChargingSlot()
            {
                ChargeStart = new DateTimeOffset(2022, 1, 10, 13, 0, 0, TimeSpan.Zero),
                ChargeEnd = new DateTimeOffset(2022, 1, 10, 13, 30, 0, TimeSpan.Zero),
            },
        };

        var combinedChargingTimeBeforeConcatenation = chargingSlots.Select(c => c.ChargeDuration.TotalHours).Sum();

        var chargeTimeCalculationService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimeCalculationService>();
        var concatenatedChargingSlots = chargeTimeCalculationService.ConcatenateChargeTimes(chargingSlots);


        Assert.Equal(combinedChargingTimeBeforeConcatenation, concatenatedChargingSlots.Select(c => c.ChargeDuration.TotalHours).Sum());
        Assert.Equal(2, concatenatedChargingSlots.Count);
    }

    [Fact]
    public void Does_Concatenate_Charging_Slots_Correctly_Partial_Hour_First()
    {
        var chargingSlots = new List<DtoChargingSlot>()
        {
            new DtoChargingSlot()
            {
                ChargeStart = new DateTimeOffset(2022, 1, 10, 10, 0, 0, TimeSpan.Zero),
                ChargeEnd = new DateTimeOffset(2022, 1, 10, 10, 25, 0, TimeSpan.Zero),
            },
            new DtoChargingSlot()
            {
                ChargeStart = new DateTimeOffset(2022, 1, 10, 11, 0, 0, TimeSpan.Zero),
                ChargeEnd = new DateTimeOffset(2022, 1, 10, 12, 0, 0, TimeSpan.Zero),
            },
        };

        var combinedChargingTimeBeforeConcatenation = chargingSlots.Select(c => c.ChargeDuration.TotalHours).Sum();

        var chargeTimeCalculationService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimeCalculationService>();
        var concatenatedChargingSlots = chargeTimeCalculationService.ReduceNumberOfSpotPricedChargingSessions(chargingSlots);


        Assert.Equal(combinedChargingTimeBeforeConcatenation, concatenatedChargingSlots.Select(c => c.ChargeDuration.TotalHours).Sum());
        Assert.Single(concatenatedChargingSlots);
    }

    //This test is based on log data from private message https://tff-forum.de/t/aw-teslasolarcharger-laden-nach-pv-ueberschuss-mit-beliebiger-wallbox/331033
    [Fact]
    public async Task Does_Use_Cheapest_Price()
    {
        var spotpricesJson =
            "[\r\n  {\r\n    \"startDate\": \"2024-02-22T18:00:00\",\r\n    \"endDate\": \"2024-02-22T19:00:00\",\r\n    \"price\": 0.05242\r\n  },\r\n  {\r\n   \"startDate\": \"2024-02-22T19:00:00\",\r\n    \"endDate\": \"2024-02-22T20:00:00\",\r\n    \"price\": 0.04245\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-22T20:00:00\",\r\n    \"endDate\": \"2024-02-22T21:00:00\",\r\n    \"price\": 0.02448\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-22T21:00:00\",\r\n    \"endDate\": \"2024-02-22T22:00:00\",\r\n    \"price\": 0.01206\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-22T22:00:00\",\r\n    \"endDate\": \"2024-02-22T23:00:00\",\r\n    \"price\": 0.00191\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-22T23:00:00\",\r\n    \"endDate\": \"2024-02-23T00:00:00\",\r\n    \"price\": 0.00923\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-23T00:00:00\",\r\n    \"endDate\": \"2024-02-23T01:00:00\",\r\n    \"price\": 0.00107\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-23T01:00:00\",\r\n    \"endDate\": \"2024-02-23T02:00:00\",\r\n    \"price\": 0.00119\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-23T02:00:00\",\r\n    \"endDate\": \"2024-02-23T03:00:00\",\r\n    \"price\": 0.00009\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-23T03:00:00\",\r\n    \"endDate\": \"2024-02-23T04:00:00\",\r\n    \"price\": 0.00002\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-23T04:00:00\",\r\n    \"endDate\": \"2024-02-23T05:00:00\",\r\n    \"price\": 0.00009\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-23T05:00:00\",\r\n    \"endDate\": \"2024-02-23T06:00:00\",\r\n    \"price\": 0.03968\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-23T06:00:00\",\r\n    \"endDate\": \"2024-02-23T07:00:00\",\r\n    \"price\": 0.05706\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-23T07:00:00\",\r\n    \"endDate\": \"2024-02-23T08:00:00\",\r\n    \"price\": 0.05935\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-23T08:00:00\",\r\n    \"endDate\": \"2024-02-23T09:00:00\",\r\n    \"price\": 0.05169\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-23T09:00:00\",\r\n    \"endDate\": \"2024-02-23T10:00:00\",\r\n    \"price\": 0.04664\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-23T10:00:00\",\r\n    \"endDate\": \"2024-02-23T11:00:00\",\r\n    \"price\": 0.04165\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-23T11:00:00\",\r\n    \"endDate\": \"2024-02-23T12:00:00\",\r\n    \"price\": 0.0371\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-23T12:00:00\",\r\n    \"endDate\": \"2024-02-23T13:00:00\",\r\n    \"price\": 0.0336\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-23T13:00:00\",\r\n    \"endDate\": \"2024-02-23T14:00:00\",\r\n    \"price\": 0.03908\r\n  },\r\n  {\r\n   \"startDate\": \"2024-02-23T14:00:00\",\r\n    \"endDate\": \"2024-02-23T15:00:00\",\r\n    \"price\": 0.04951\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-23T15:00:00\",\r\n    \"endDate\": \"2024-02-23T16:00:00\",\r\n    \"price\": 0.06308\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-23T16:00:00\",\r\n    \"endDate\": \"2024-02-23T17:00:00\",\r\n    \"price\": 0.0738\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-23T17:00:00\",\r\n    \"endDate\": \"2024-02-23T18:00:00\",\r\n    \"price\": 0.08644\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-23T18:00:00\",\r\n    \"endDate\": \"2024-02-23T19:00:00\",\r\n    \"price\": 0.08401\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-23T19:00:00\",\r\n    \"endDate\": \"2024-02-23T20:00:00\",\r\n    \"price\": 0.07297\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-23T20:00:00\",\r\n    \"endDate\": \"2024-02-23T21:00:00\",\r\n    \"price\": 0.06926\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-23T21:00:00\",\r\n    \"endDate\": \"2024-02-23T22:00:00\",\r\n    \"price\": 0.06798\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-23T22:00:00\",\r\n    \"endDate\": \"2024-02-23T23:00:00\",\r\n    \"price\": 0.0651\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-23T23:00:00\",\r\n    \"endDate\": \"2024-02-24T00:00:00\",\r\n    \"price\": 0.06647\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-24T00:00:00\",\r\n    \"endDate\": \"2024-02-24T01:00:00\",\r\n    \"price\": 0.0639\r\n  },\r\n  {\r\n    \"startDate\": \"2024-02-24T01:00:00\",\r\n    \"endDate\": \"2024-02-24T02:00:00\",\r\n    \"price\": 0.0595\r\n  }\r\n]";
        var spotPricesToAddToDb = JsonConvert.DeserializeObject<List<SpotPrice>>(spotpricesJson);
        Assert.NotNull(spotPricesToAddToDb);
        Context.SpotPrices.AddRange(spotPricesToAddToDb);
        await Context.SaveChangesAsync();
        var chargeTimeCalculationService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimeCalculationService>();
        var carJson =
            "{\"Id\":1,\"Vin\":\"LRW3E7FS2NC\",\"CarConfiguration\":{\"ChargeMode\":3,\"MinimumSoC\":80,\"LatestTimeToReachSoC\":\"2024-02-23T15:30:00\",\"IgnoreLatestTimeToReachSocDate\":false,\"MaximumAmpere\":16,\"MinimumAmpere\":1,\"UsableEnergy\":58,\"ShouldBeManaged\":true,\"ShouldSetChargeStartTimes\":true,\"ChargingPriority\":1},\"CarState\":{\"Name\":\"Model 3\",\"ShouldStartChargingSince\":null,\"EarliestSwitchOn\":null,\"ShouldStopChargingSince\":\"2024-02-22T13:01:37.0448677+01:00\",\"EarliestSwitchOff\":\"2024-02-22T13:06:37.0448677+01:00\",\"ScheduledChargingStartTime\":\"2024-02-24T01:45:00+00:00\",\"SoC\":58,\"SocLimit\":100,\"IsHomeGeofence\":true,\"TimeUntilFullCharge\":\"02:45:00\",\"ReachingMinSocAtFullSpeedCharge\":\"2024-02-23T06:09:34.4100825+01:00\",\"AutoFullSpeedCharge\":true,\"LastSetAmp\":16,\"ChargerPhases\":2,\"ActualPhases\":3,\"ChargerVoltage\":228,\"ChargerActualCurrent\":16,\"ChargerPilotCurrent\":16,\"ChargerRequestedCurrent\":16,\"PluggedIn\":true,\"ClimateOn\":false,\"DistanceToHomeGeofence\":-19,\"ChargingPowerAtHome\":10944,\"State\":3,\"Healthy\":true,\"ReducedChargeSpeedWarning\":false,\"PlannedChargingSlots\":[{\"ChargeStart\":\"2024-02-23T12:00:00+00:00\",\"ChargeEnd\":\"2024-02-23T12:09:34.4150924+00:00\",\"IsActive\":false,\"ChargeDuration\":\"00:09:34.4150924\"},{\"ChargeStart\":\"2024-02-23T02:43:07.0475086+01:00\",\"ChargeEnd\":\"2024-02-23T06:00:00+01:00\",\"IsActive\":true,\"ChargeDuration\":\"03:16:52.9524914\"}]}}";
        var car = JsonConvert.DeserializeObject<DtoCar>(carJson);
        Assert.NotNull(car);
        Mock.Mock<ISettings>().Setup(ds => ds.Cars).Returns(new List<DtoCar>() { car });
        var dateTimeOffsetNow = new DateTimeOffset(2024, 2, 23, 5, 0, 1, TimeSpan.FromHours(1));
        Mock.Mock<IDateTimeProvider>().Setup(ds => ds.DateTimeOffSetNow()).Returns(dateTimeOffsetNow);
        Mock.Mock<ISpotPriceService>()
            .Setup(ds => ds.LatestKnownSpotPriceTime())
            .Returns(Task.FromResult(new DateTimeOffset(spotPricesToAddToDb.OrderByDescending(p => p.EndDate).Select(p => p.EndDate).First(), TimeSpan.Zero)));
        var chargingSlots = await chargeTimeCalculationService.GenerateSpotPriceChargingSlots(car,
            TimeSpan.FromMinutes(69), dateTimeOffsetNow,
            new DateTimeOffset(car.LatestTimeToReachSoC, TimeSpan.FromHours(1)));
    }

    [Fact]
    public void Does_Concatenate_Charging_Slots_Correctly_Partial_Hour_Last()
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
                ChargeEnd = new DateTimeOffset(2022, 1, 10, 11, 20, 0, TimeSpan.Zero),
            },
        };

        var combinedChargingTimeBeforeConcatenation = chargingSlots.Select(c => c.ChargeDuration.TotalHours).Sum();

        var chargeTimeCalculationService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimeCalculationService>();
        var concatenatedChargingSlots = chargeTimeCalculationService.ConcatenateChargeTimes(chargingSlots);


        Assert.Equal(combinedChargingTimeBeforeConcatenation, concatenatedChargingSlots.Select(c => c.ChargeDuration.TotalHours).Sum());
        Assert.Single(concatenatedChargingSlots);
    }

    [Fact]
    public void Does_Concatenate_Charging_Slots_Correctly_Partial_Hour_Middle()
    {
        var chargingSlots = new List<DtoChargingSlot>()
        {
            new DtoChargingSlot()
            {
                ChargeStart = new DateTimeOffset(2022, 1, 11, 20, 0, 0, TimeSpan.Zero),
                ChargeEnd = new DateTimeOffset(2022, 1, 11, 21, 0, 0, TimeSpan.Zero),
            },
            new DtoChargingSlot()
            {
                ChargeStart = new DateTimeOffset(2022, 1, 11, 21, 0, 0, TimeSpan.Zero),
                ChargeEnd = new DateTimeOffset(2022, 1, 11, 21, 35, 0, TimeSpan.Zero),
            },
            new DtoChargingSlot()
            {
                ChargeStart = new DateTimeOffset(2022, 1, 11, 22, 0, 0, TimeSpan.Zero),
                ChargeEnd = new DateTimeOffset(2022, 1, 11, 23, 0, 0, TimeSpan.Zero),
            },
        };

        var combinedChargingTimeBeforeConcatenation = chargingSlots.Select(c => c.ChargeDuration.TotalHours).Sum();

        var chargeTimeCalculationService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimeCalculationService>();
        var concatenatedChargingSlots = chargeTimeCalculationService.ReduceNumberOfSpotPricedChargingSessions(chargingSlots);


        Assert.Equal(combinedChargingTimeBeforeConcatenation,
            concatenatedChargingSlots.Select(c => c.ChargeDuration.TotalHours).Sum(),
            0.001);
        Assert.Equal(2, concatenatedChargingSlots.Count);
    }

    [Fact]
    public void Does_Concatenate_Charging_Slots_Correctly_Start_Charge_Now()
    {
        var chargingSlots = new List<DtoChargingSlot>()
        {
            new DtoChargingSlot()
            {
                ChargeStart = new DateTimeOffset(2022, 1, 10, 10, 10, 0, TimeSpan.Zero),
                ChargeEnd = new DateTimeOffset(2022, 1, 10, 10, 43, 0, TimeSpan.Zero),
            },
            new DtoChargingSlot()
            {
                ChargeStart = new DateTimeOffset(2022, 1, 10, 11, 0, 0, TimeSpan.Zero),
                ChargeEnd = new DateTimeOffset(2022, 1, 10, 12, 0, 0, TimeSpan.Zero),
            },
        };

        var combinedChargingTimeBeforeConcatenation = chargingSlots.Select(c => c.ChargeDuration.TotalHours).Sum();

        var chargeTimeCalculationService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimeCalculationService>();
        var concatenatedChargingSlots = chargeTimeCalculationService.ReduceNumberOfSpotPricedChargingSessions(chargingSlots);


        Assert.Equal(combinedChargingTimeBeforeConcatenation, concatenatedChargingSlots.Select(c => c.ChargeDuration.TotalHours).Sum());
        Assert.Single(concatenatedChargingSlots);
    }

    [Fact]
    public void Does_Concatenate_Charging_Slots_Correctly_Start_Charge_Before_Midnight()
    {
        var chargingSlots = new List<DtoChargingSlot>()
        {
            new DtoChargingSlot()
            {
                ChargeStart = new DateTimeOffset(2022, 1, 10, 23, 0, 0, TimeSpan.Zero),
                ChargeEnd = new DateTimeOffset(2022, 1, 10, 23, 30, 0, TimeSpan.Zero),
            },
            new DtoChargingSlot()
            {
                ChargeStart = new DateTimeOffset(2022, 1, 11, 0, 0, 0, TimeSpan.Zero),
                ChargeEnd = new DateTimeOffset(2022, 1, 11, 1, 0, 0, TimeSpan.Zero),
            },
        };

        var combinedChargingTimeBeforeConcatenation = chargingSlots.Select(c => c.ChargeDuration.TotalHours).Sum();

        var chargeTimeCalculationService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimeCalculationService>();
        var concatenatedChargingSlots = chargeTimeCalculationService.ReduceNumberOfSpotPricedChargingSessions(chargingSlots);


        Assert.Equal(combinedChargingTimeBeforeConcatenation, concatenatedChargingSlots.Select(c => c.ChargeDuration.TotalHours).Sum());
        Assert.Single(concatenatedChargingSlots);
    }

    [Theory, MemberData(nameof(CalculateCorrectChargeTimesWithoutStockPricesData))]
    public async Task Calculate_Correct_ChargeTimes_Without_Stock_Prices(ChargeMode chargeMode, DateTime latestTimeToReachSoc, DateTimeOffset currentDate, DateTimeOffset expectedStart)
    {
        var chargeDuration = TimeSpan.FromHours(1);

        var car = new DtoCar
        {
                ChargeMode = chargeMode,
                LatestTimeToReachSoC = latestTimeToReachSoc,
                MinimumSoC = 47,
                UsableEnergy = 50000,
                MaximumAmpere = 15215,
                SoC = 40,
                ChargerPhases = 1,
        };

        var chargeTimeCalculationService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimeCalculationService>();
        // ReSharper disable once UseConfigureAwaitFalse
        var chargingSlots = await chargeTimeCalculationService.PlanChargingSlots(car, currentDate);

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
            Assert.InRange(plannedChargingSlot.ChargeDuration, chargeDuration.Add(-maximumErrorTime), chargeDuration.Add(maximumErrorTime));
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
