using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedBackend.Contracts;
using Xunit;
using Xunit.Abstractions;

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
        var car = new Car()
        {
            CarConfiguration = new CarConfiguration()
            {
                MinimumSoC = minimumSoc,
                UsableEnergy = usableEnergy,
                MaximumAmpere = maximumAmpere,
            },
            CarState = new CarState()
            {
                SoC = acutalSoc,
                ChargerPhases = chargerPhases,
                State = carState,
            },
        };

        var chargeTimeCalculationService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimeCalculationService>();
        Mock.Mock<IConstants>().Setup(c => c.MinimumSocDifference).Returns(2);
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
        var chargeTimeCalculationService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimeCalculationService>();

        chargeTimeCalculationService.UpdateChargeTime(car);

        var lowerMinutes = 60 * (3 / numberOfPhases);

#pragma warning disable CS8629
        Assert.InRange((DateTime)car.CarState.ReachingMinSocAtFullSpeedCharge, dateTime.AddMinutes(lowerMinutes), dateTime.AddMinutes(lowerMinutes + 1));
#pragma warning restore CS8629
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
        var chargeTimeCalculationService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimeCalculationService>();

        chargeTimeCalculationService.UpdateChargeTime(car);

        Assert.Equal(dateTime, car.CarState.ReachingMinSocAtFullSpeedCharge);
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
        var concatenatedChargingSlots = chargeTimeCalculationService.ConcatenateChargeTimes(chargingSlots);


        Assert.Equal(combinedChargingTimeBeforeConcatenation, concatenatedChargingSlots.Select(c => c.ChargeDuration.TotalHours).Sum());
        Assert.Single(concatenatedChargingSlots);
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
        var concatenatedChargingSlots = chargeTimeCalculationService.ConcatenateChargeTimes(chargingSlots);


        Assert.Equal(combinedChargingTimeBeforeConcatenation, concatenatedChargingSlots.Select(c => c.ChargeDuration.TotalHours).Sum());
        Assert.Single(concatenatedChargingSlots);
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
        var concatenatedChargingSlots = chargeTimeCalculationService.ConcatenateChargeTimes(chargingSlots);


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
        var concatenatedChargingSlots = chargeTimeCalculationService.ConcatenateChargeTimes(chargingSlots);


        Assert.Equal(combinedChargingTimeBeforeConcatenation, concatenatedChargingSlots.Select(c => c.ChargeDuration.TotalHours).Sum());
        Assert.Single(concatenatedChargingSlots);
    }

    [Theory, MemberData(nameof(CalculateCorrectChargeTimesWithoutStockPricesData))]
    public async Task Calculate_Correct_ChargeTimes_Without_Stock_Prices(ChargeMode chargeMode, DateTime latestTimeToReachSoc, DateTimeOffset currentDate, DateTimeOffset expectedStart)
    {
        var chargeDuration = TimeSpan.FromHours(1);

        var car = new Car
        {
            CarConfiguration = new CarConfiguration
            {
                ChargeMode = chargeMode,
                LatestTimeToReachSoC = latestTimeToReachSoc,
                MinimumSoC = 47,
                UsableEnergy = 50000,
                MaximumAmpere = 15215,
            },
            CarState = new CarState()
            {
                SoC = 40,
                ChargerPhases = 1,
            },
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
