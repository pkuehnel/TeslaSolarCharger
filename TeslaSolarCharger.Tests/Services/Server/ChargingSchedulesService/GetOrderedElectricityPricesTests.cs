using System;
using System.Collections.Generic;
using Autofac.Extras.Moq;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.GridPrice.Dtos;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Enums;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingSchedulesService;

public class GetOrderedElectricityPricesTests : TestBase
{
    private readonly ITestOutputHelper _outputHelper;

    public GetOrderedElectricityPricesTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
        _outputHelper = outputHelper;
    }

    [Theory]
    [MemberData(nameof(GetOrderedElectricityPricesTestData))]
    public void GetOrderedElectricityPrices_ReturnsExpectedOrderedPrices(
        string description,
        DateTimeOffset currentDate,
        List<Price> splittedGridPrices,
        bool isCurrentlyCharging,
        List<DtoChargingSchedule> splittedChargingSchedules,
        decimal chargingSwitchCosts,
        int maxPower,
        List<decimal> expectedPrices)
    {
        _outputHelper.WriteLine($"Running Scenario: {description}");

        // Arrange
        var service = Mock.Create<ChargingScheduleService>();

        // Act
        var result = service.GetOrderedElectricityPrices(
            currentDate,
            splittedGridPrices,
            isCurrentlyCharging,
            splittedChargingSchedules,
            chargingSwitchCosts,
            maxPower);

        // Assert
        Assert.Equal(expectedPrices.Count, result.Count);
        for (int i = 0; i < expectedPrices.Count; i++)
        {
            // Allow small precision differences if any calculation happened
            Assert.Equal(expectedPrices[i], result[i].GridPrice);
        }
    }

    public static IEnumerable<object[]> GetOrderedElectricityPricesTestData()
    {
        var baseDate = new DateTimeOffset(2023, 10, 27, 12, 0, 0, TimeSpan.Zero);

        // Scenario 1: Basic case with switch costs
        // 10 switch cost, 10kW max power => 1 per kWh switch cost (for 1 hour)
        yield return new object[]
        {
            "Basic case: No charging, no schedules. Switch costs applied.",
            baseDate,
            new List<Price>
            {
                new Price { ValidFrom = baseDate, ValidTo = baseDate.AddHours(1), GridPrice = 0.20m },
                new Price { ValidFrom = baseDate.AddHours(1), ValidTo = baseDate.AddHours(2), GridPrice = 0.15m }
            },
            false,
            new List<DtoChargingSchedule>(),
            10m,
            10000,
            new List<decimal> { 1.15m, 1.20m }
        };

        // Scenario 2: Currently charging
        // First slot overlaps with currentDate, so no switch cost.
        yield return new object[]
        {
            "Currently charging: No switch cost for current slot.",
            baseDate.AddMinutes(5),
            new List<Price>
            {
                new Price { ValidFrom = baseDate, ValidTo = baseDate.AddHours(1), GridPrice = 0.20m },
                new Price { ValidFrom = baseDate.AddHours(1), ValidTo = baseDate.AddHours(2), GridPrice = 0.15m }
            },
            true,
            new List<DtoChargingSchedule>(),
            10m,
            10000,
            new List<decimal> { 0.20m, 1.15m }
        };

        // Scenario 3: Adjacent to existing schedule
        // Schedule at 14:00-15:00.
        // Slot 13:00-14:00 is adjacent -> No switch cost.
        yield return new object[]
        {
            "Adjacent schedule: No switch cost for adjacent slot.",
            baseDate,
            new List<Price>
            {
                new Price { ValidFrom = baseDate, ValidTo = baseDate.AddHours(1), GridPrice = 0.20m }, // 12-13
                new Price { ValidFrom = baseDate.AddHours(1), ValidTo = baseDate.AddHours(2), GridPrice = 0.15m } // 13-14
            },
            false,
            new List<DtoChargingSchedule>
            {
                new DtoChargingSchedule(1, 1, 10000, 230, 3, new HashSet<ScheduleReason>())
                {
                    ValidFrom = baseDate.AddHours(2), // 14:00
                    ValidTo = baseDate.AddHours(3), // 15:00
                    TargetMinPower = 10000 // Charging
                }
            },
            10m,
            10000,
            new List<decimal> { 0.15m, 1.20m }
        };

        // Scenario 4: Overlapping existing schedule
        yield return new object[]
        {
            "Overlapping schedule: No switch cost.",
            baseDate,
            new List<Price>
            {
                 new Price { ValidFrom = baseDate.AddHours(2), ValidTo = baseDate.AddHours(3), GridPrice = 0.10m } // 14-15
            },
            false,
            new List<DtoChargingSchedule>
            {
                new DtoChargingSchedule(1, 1, 10000, 230, 3, new HashSet<ScheduleReason>())
                {
                    ValidFrom = baseDate.AddHours(2), // 14:00
                    ValidTo = baseDate.AddHours(3), // 15:00
                    TargetMinPower = 10000
                }
            },
            10m,
            10000,
            new List<decimal> { 0.10m }
        };

        // Scenario 5: Schedule exists but TargetMinPower is 0 (not charging)
        // Should apply switch cost because we need to switch ON to charge.
        yield return new object[]
        {
            "Existing schedule but 0 power: Switch cost applied.",
            baseDate,
            new List<Price>
            {
                new Price { ValidFrom = baseDate.AddHours(1), ValidTo = baseDate.AddHours(2), GridPrice = 0.15m } // 13-14
            },
            false,
            new List<DtoChargingSchedule>
            {
                new DtoChargingSchedule(1, 1, 10000, 230, 3, new HashSet<ScheduleReason>())
                {
                    ValidFrom = baseDate.AddHours(2),
                    ValidTo = baseDate.AddHours(3),
                    TargetMinPower = 0 // Not charging
                }
            },
            10m,
            10000,
            new List<decimal> { 1.15m }
        };

         // Scenario 6: High Switch Costs making cheaper price expensive
         yield return new object[]
        {
            "High switch cost changes order.",
            baseDate,
            new List<Price>
            {
                new Price { ValidFrom = baseDate, ValidTo = baseDate.AddHours(1), GridPrice = 0.20m }, // 12-13
                new Price { ValidFrom = baseDate.AddHours(1), ValidTo = baseDate.AddHours(2), GridPrice = 0.05m } // 13-14 (Cheap but needs switch)
            },
            true, // Currently charging at 12:00
            new List<DtoChargingSchedule>(),
            30m, // 3 per kWh -> 3.0 switch cost
            10000,
            // 12-13: 0.20 (no switch cost)
            // 13-14: 0.05 + 3.0 = 3.05
            new List<decimal> { 0.20m, 3.05m }
        };
    }
}
