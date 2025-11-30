using System;
using System.Collections.Generic;
using TeslaSolarCharger.Shared.Dtos;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingSchedulesService;

public class GetRemainingEnergyToChargeTests : TestBase
{
    public GetRemainingEnergyToChargeTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    /// <summary>
    /// Verifies that GetRemainingEnergyToCharge correctly calculates the remaining energy to charge
    /// by subtracting the energy of existing schedules that fall within the time window.
    /// It handles various overlap scenarios and multiple schedules.
    /// </summary>
    /// <param name="currentDateOffsetMin">Offset in minutes from CurrentFakeDate for the current time.</param>
    /// <param name="targetDateOffsetMin">Offset in minutes from CurrentFakeDate for the target execution time.</param>
    /// <param name="energyToCharge">The initial total energy required to charge (Wh).</param>
    /// <param name="s1Start">Start offset (min) for the first schedule (optional).</param>
    /// <param name="s1End">End offset (min) for the first schedule (optional).</param>
    /// <param name="s1Power">Power (W) for the first schedule (optional).</param>
    /// <param name="s2Start">Start offset (min) for the second schedule (optional).</param>
    /// <param name="s2End">End offset (min) for the second schedule (optional).</param>
    /// <param name="s2Power">Power (W) for the second schedule (optional).</param>
    /// <param name="expectedRemainingEnergy">The expected remaining energy to charge (Wh).</param>
    [Theory]
    // 1. No schedules: Remaining energy should equal initial energy.
    [InlineData(0, 60, 1000, null, null, null, null, null, null, 1000)]

    // 2. Schedule completely before window: Should be ignored.
    // Window: 60-120. Schedule: 0-30.
    [InlineData(60, 120, 1000, 0, 30, 1000, null, null, null, 1000)]

    // 3. Schedule completely after window: Should be ignored.
    // Window: 60-120. Schedule: 150-180.
    [InlineData(60, 120, 1000, 150, 180, 1000, null, null, null, 1000)]

    // 4. Schedule completely inside window.
    // Window: 60-120. Schedule: 70-80 (10 mins). Power: 6000W.
    // Scheduled Energy: 10/60 h * 6000 W = 1000 Wh.
    // Remaining: 2000 - 1000 = 1000 Wh.
    [InlineData(60, 120, 2000, 70, 80, 6000, null, null, null, 1000)]

    // 5. Schedule overlaps start of window.
    // Window: 60-120. Schedule: 30-90.
    // Overlap: 60-90 (30 mins). Power: 2000W.
    // Scheduled Energy: 0.5 h * 2000 W = 1000 Wh.
    // Remaining: 2000 - 1000 = 1000 Wh.
    [InlineData(60, 120, 2000, 30, 90, 2000, null, null, null, 1000)]

    // 6. Schedule overlaps end of window.
    // Window: 60-120. Schedule: 90-150.
    // Overlap: 90-120 (30 mins). Power: 2000W.
    // Scheduled Energy: 0.5 h * 2000 W = 1000 Wh.
    // Remaining: 2000 - 1000 = 1000 Wh.
    [InlineData(60, 120, 2000, 90, 150, 2000, null, null, null, 1000)]

    // 7. Schedule envelops window.
    // Window: 60-120. Schedule: 30-150.
    // Overlap: 60-120 (60 mins). Power: 1000W.
    // Scheduled Energy: 1 h * 1000 W = 1000 Wh.
    // Remaining: 2000 - 1000 = 1000 Wh.
    [InlineData(60, 120, 2000, 30, 150, 1000, null, null, null, 1000)]

    // 8. Multiple schedules disjoint within window.
    // Window: 0-120.
    // S1: 0-60 (1h), 1000W -> 1000 Wh.
    // S2: 60-120 (1h), 1000W -> 1000 Wh.
    // Total Scheduled: 2000 Wh.
    // Remaining: 3000 - 2000 = 1000 Wh.
    [InlineData(0, 120, 3000, 0, 60, 1000, 60, 120, 1000, 1000)]

    // 9. Multiple schedules with one outside.
    // Window: 60-120.
    // S1: 0-30 (Outside) -> 0 Wh.
    // S2: 60-90 (Inside 30m), 2000W -> 1000 Wh.
    // Remaining: 2000 - 1000 = 1000 Wh.
    [InlineData(60, 120, 2000, 0, 30, 1000, 60, 90, 2000, 1000)]

    // 10. Negative remaining energy (Over-scheduled).
    // Window: 60-120. S1: 60-120 (1h), 2000W -> 2000 Wh.
    // Required: 1000 Wh.
    // Remaining: 1000 - 2000 = -1000 Wh.
    [InlineData(60, 120, 1000, 60, 120, 2000, null, null, null, -1000)]

    // 11. Inverted Window (Target < Current).
    // Should result in 0 scheduled energy calculated, return initial energy.
    [InlineData(120, 60, 1000, 0, 200, 1000, null, null, null, 1000)]

    public void GetRemainingEnergyToCharge_Scenarios(
        int currentDateOffsetMin,
        int targetDateOffsetMin,
        int energyToCharge,
        int? s1Start, int? s1End, int? s1Power,
        int? s2Start, int? s2End, int? s2Power,
        int expectedRemainingEnergy)
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var currentDate = CurrentFakeDate.AddMinutes(currentDateOffsetMin);
        var targetDate = CurrentFakeDate.AddMinutes(targetDateOffsetMin);

        var schedules = new List<DtoChargingSchedule>();
        if (s1Start.HasValue && s1End.HasValue && s1Power.HasValue)
        {
            schedules.Add(CreateSchedule(s1Start.Value, s1End.Value, s1Power.Value));
        }
        if (s2Start.HasValue && s2End.HasValue && s2Power.HasValue)
        {
            schedules.Add(CreateSchedule(s2Start.Value, s2End.Value, s2Power.Value));
        }

        // Act
        var result = service.GetRemainingEnergyToCharge(currentDate, schedules, targetDate, energyToCharge);

        // Assert
        Assert.Equal(expectedRemainingEnergy, result);
    }

    private DtoChargingSchedule CreateSchedule(int startOffsetMin, int endOffsetMin, int power)
    {
        return new DtoChargingSchedule
        {
            ValidFrom = CurrentFakeDate.AddMinutes(startOffsetMin),
            ValidTo = CurrentFakeDate.AddMinutes(endOffsetMin),
            MaxPossiblePower = power,
            TargetMinPower = power
        };
    }
}
