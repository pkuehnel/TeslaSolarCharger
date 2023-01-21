using Moq;
using System;
using System.Linq;
using System.Threading.Tasks;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

public class ChargeTimePlanningService : TestBase
{
    public ChargeTimePlanningService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Theory]
    [InlineData(1.2, 1)]
    [InlineData(1.0, 1)]
    [InlineData(0.9, 0)]
    [InlineData(27.7, 27)]
    public void Gets_Correct_Full_Hours(double totalHours, int expectedValue)
    {
        var chargeTimePlanningService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimePlanningService>();
        var result = chargeTimePlanningService.GetFullNeededHours(totalHours);

        Assert.Equal(expectedValue, result);
    }

    [Theory]
    [InlineData(ChargeMode.PvAndMinSoc)]
    [InlineData(ChargeMode.PvOnly)]
    public async Task Dont_Plan_Charging_If_Min_Soc_Reached(ChargeMode chargeMode)
    {
        var chargeDuration = TimeSpan.Zero;

        Mock.Mock<IChargeTimeUpdateService>()
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

        var chargeTimePlanningService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimePlanningService>();
        var chargingSlots = await chargeTimePlanningService.PlanChargingSlots(car, currentDate).ConfigureAwait(false);

        Assert.Empty(chargingSlots);
    }

    [Theory, MemberData(nameof(CalculateCorrectChargeTimesWithoutStockPricesData))]
    public async Task Calculate_Correct_ChargeTimes_Without_Stock_Prices(ChargeMode chargeMode, DateTime latestTimeToReachSoc, DateTimeOffset currentDate, DateTimeOffset expectedStart)
    {
        var chargeDuration = TimeSpan.FromHours(1);

        Mock.Mock<IChargeTimeUpdateService>()
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

        var chargeTimePlanningService = Mock.Create<TeslaSolarCharger.Server.Services.ChargeTimePlanningService>();
        var chargingSlots = await chargeTimePlanningService.PlanChargingSlots(car, currentDate).ConfigureAwait(false);

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
