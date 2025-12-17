using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.GridPrice.Dtos;
using TeslaSolarCharger.Shared.Enums;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

public class TscOnlyChargingCostServiceTests : TestBase
{
    public TscOnlyChargingCostServiceTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Theory]
    [MemberData(nameof(GetSpotPriceScenarios))]
    public async Task AddSpotPrices_ReturnsCorrectPrices_DependingOnAvailability(
        string description,
        DateTimeOffset requestFrom,
        DateTimeOffset requestTo,
        List<(DateTimeOffset Start, decimal Price)> spotPricesData,
        List<(DateTimeOffset From, DateTimeOffset To)> expectedPricePeriods)
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TscOnlyChargingCostService>();

        // Setup base prices covering the whole request
        var basePrices = new List<Price>
        {
            new Price { ValidFrom = requestFrom, ValidTo = requestTo, GridPrice = 0.10m, SolarPrice = 0.05m }
        };

        var chargePrice = new ChargePrice
        {
            Id = 1,
            SpotPriceRegion = SpotPriceRegion.DE_LU,
            SpotPriceCorrectionFactor = 0,
            AddSpotPriceToGridPrice = true
        };

        // Populate Spot Prices in InMemory DB
        foreach (var sp in spotPricesData)
        {
            Context.SpotPrices.Add(new SpotPrice
            {
                SpotPriceRegion = SpotPriceRegion.DE_LU,
                StartDate = sp.Start.UtcDateTime,
                Price = sp.Price
            });
        }
        await Context.SaveChangesAsync();

        // Act
        var result = await service.AddSpotPrices(basePrices, requestFrom, requestTo, chargePrice);

        // Assert
        Assert.NotNull(result);

        // Verify the number of price segments matches expected
        // If the bug exists (code filling gaps), result.Count will be greater than expectedPricePeriods.Count
        // or the periods will just be wrong (e.g. one big block instead of split blocks).
        Assert.Equal(expectedPricePeriods.Count, result.Count);

        for (int i = 0; i < expectedPricePeriods.Count; i++)
        {
            var expected = expectedPricePeriods[i];
            var actual = result[i];

            Assert.Equal(expected.From, actual.ValidFrom);
            Assert.Equal(expected.To, actual.ValidTo);
        }
    }

    public static TheoryData<string, DateTimeOffset, DateTimeOffset, List<(DateTimeOffset Start, decimal Price)>, List<(DateTimeOffset From, DateTimeOffset To)>> GetSpotPriceScenarios()
    {
        var data = new TheoryData<string, DateTimeOffset, DateTimeOffset, List<(DateTimeOffset Start, decimal Price)>, List<(DateTimeOffset From, DateTimeOffset To)>>();

        var baseDate = new DateTimeOffset(2023, 10, 1, 10, 0, 0, TimeSpan.Zero); // 10:00 UTC
        var interval = TimeSpan.FromMinutes(15);

        // Helper to generate historical points for slice detection
        // We need 2 points BEFORE requestFrom.
        // requestFrom is 10:00.
        // We add points at 09:30 and 09:45.
        var history = new List<(DateTimeOffset, decimal)>
        {
            (baseDate.AddMinutes(-30), 0.10m),
            (baseDate.AddMinutes(-15), 0.11m)
        };

        // Scenario 1: Full Coverage (10:00 - 10:30) - 2 intervals
        // Request: 10:00 - 10:30
        // Spots: 10:00 (10:00-10:15), 10:15 (10:15-10:30)
        var spots1 = new List<(DateTimeOffset, decimal)>(history)
        {
            (baseDate, 0.20m),
            (baseDate.AddMinutes(15), 0.21m)
        };
        data.Add(
            "Full Coverage",
            baseDate,
            baseDate.AddMinutes(30),
            spots1,
            new List<(DateTimeOffset, DateTimeOffset)>
            {
                (baseDate, baseDate.AddMinutes(15)),
                (baseDate.AddMinutes(15), baseDate.AddMinutes(30))
            }
        );

        // Scenario 2: No Spot Prices in range (but history exists)
        // Request: 10:00 - 10:30
        // Spots: Only history (stops at 09:45)
        // Expectation: Empty list (because no spots cover 10:00+)
        var spots2 = new List<(DateTimeOffset, decimal)>(history);
        data.Add(
            "No Spot Prices",
            baseDate,
            baseDate.AddMinutes(30),
            spots2,
            new List<(DateTimeOffset, DateTimeOffset)>()
        );

        // Scenario 3: Partial Coverage - Missing Start
        // Request: 10:00 - 10:30
        // Spots: History... then Gap 10:00-10:15... then Spot 10:15-10:30
        // Expectation: Only 10:15-10:30 should be returned.
        var spots3 = new List<(DateTimeOffset, decimal)>(history)
        {
            // Missing 10:00
            (baseDate.AddMinutes(15), 0.21m)
        };
        data.Add(
            "Missing Start",
            baseDate,
            baseDate.AddMinutes(30),
            spots3,
            new List<(DateTimeOffset, DateTimeOffset)>
            {
                (baseDate.AddMinutes(15), baseDate.AddMinutes(30))
            }
        );

        // Scenario 4: Partial Coverage - Missing End
        // Request: 10:00 - 10:30
        // Spots: 10:00 (10:00-10:15). Missing 10:15.
        // Expectation: Only 10:00-10:15 returned.
        var spots4 = new List<(DateTimeOffset, decimal)>(history)
        {
            (baseDate, 0.20m)
            // Missing 10:15
        };
        data.Add(
            "Missing End",
            baseDate,
            baseDate.AddMinutes(30),
            spots4,
            new List<(DateTimeOffset, DateTimeOffset)>
            {
                (baseDate, baseDate.AddMinutes(15))
            }
        );

        // Scenario 5: Gap in Middle
        // Request: 10:00 - 10:45
        // Spots: 10:00 (10-10:15), [Gap 10:15-10:30], 10:30 (10:30-10:45)
        // Expectation: 10:00-10:15, 10:30-10:45.
        var spots5 = new List<(DateTimeOffset, decimal)>(history)
        {
            (baseDate, 0.20m),
            // Missing 10:15
            (baseDate.AddMinutes(30), 0.22m)
        };
        data.Add(
            "Gap in Middle",
            baseDate,
            baseDate.AddMinutes(45),
            spots5,
            new List<(DateTimeOffset, DateTimeOffset)>
            {
                (baseDate, baseDate.AddMinutes(15)),
                (baseDate.AddMinutes(30), baseDate.AddMinutes(45))
            }
        );

        return data;
    }
}
