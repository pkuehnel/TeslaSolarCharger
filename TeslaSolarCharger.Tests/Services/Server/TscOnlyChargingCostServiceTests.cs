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
        Assert.Equal(expectedPricePeriods.Count, result.Count);

        for (int i = 0; i < expectedPricePeriods.Count; i++)
        {
            var expected = expectedPricePeriods[i];
            var actual = result[i];

            Assert.Equal(expected.From, actual.ValidFrom);
            Assert.Equal(expected.To, actual.ValidTo);
        }
    }

    [Fact]
    public async Task AddSpotPrices_CalculatesPricesCorrectly()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TscOnlyChargingCostService>();
        var requestFrom = new DateTimeOffset(2023, 10, 1, 10, 0, 0, TimeSpan.Zero);
        var requestTo = requestFrom.AddMinutes(30);

        var baseGridPrice = 0.10m;
        var spotPriceValue = 0.20m;
        var correctionFactor = 0.1m; // 10%

        var basePrices = new List<Price>
        {
            new Price { ValidFrom = requestFrom, ValidTo = requestTo, GridPrice = baseGridPrice, SolarPrice = 0.05m }
        };

        var chargePrice = new ChargePrice
        {
            Id = 1,
            SpotPriceRegion = SpotPriceRegion.DE_LU,
            SpotPriceCorrectionFactor = correctionFactor,
            AddSpotPriceToGridPrice = true
        };

        // Add history (needed for slice detection)
        Context.SpotPrices.Add(new SpotPrice { SpotPriceRegion = SpotPriceRegion.DE_LU, StartDate = requestFrom.AddMinutes(-30).UtcDateTime, Price = 0.10m });
        Context.SpotPrices.Add(new SpotPrice { SpotPriceRegion = SpotPriceRegion.DE_LU, StartDate = requestFrom.AddMinutes(-15).UtcDateTime, Price = 0.11m });
        // Add actual spot prices
        Context.SpotPrices.Add(new SpotPrice { SpotPriceRegion = SpotPriceRegion.DE_LU, StartDate = requestFrom.UtcDateTime, Price = spotPriceValue });
        Context.SpotPrices.Add(new SpotPrice { SpotPriceRegion = SpotPriceRegion.DE_LU, StartDate = requestFrom.AddMinutes(15).UtcDateTime, Price = spotPriceValue });

        await Context.SaveChangesAsync();

        // Act
        var result = await service.AddSpotPrices(basePrices, requestFrom, requestTo, chargePrice);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        // Expected calculation:
        // SpotAddition = SpotPrice + (SpotPrice * CorrectionFactor)
        // GridPrice = BaseGridPrice + SpotAddition
        //           = 0.10 + (0.20 + 0.20 * 0.10)
        //           = 0.10 + (0.20 + 0.02)
        //           = 0.10 + 0.22
        //           = 0.32
        var expectedGridPrice = baseGridPrice + (spotPriceValue + (spotPriceValue * correctionFactor));

        foreach (var price in result)
        {
            Assert.Equal(expectedGridPrice, price.GridPrice);
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
        var spots2 = new List<(DateTimeOffset, decimal)>(history);
        data.Add(
            "No Spot Prices",
            baseDate,
            baseDate.AddMinutes(30),
            spots2,
            new List<(DateTimeOffset, DateTimeOffset)>()
        );

        // Scenario 3: Partial Coverage - Missing Start
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

        // Scenario 6: Insufficient History (Fewer than 2 points)
        // Request: 10:00 - 10:30
        // Spots: Only 1 point in history (or none).
        // Expectation: Empty list because slice length cannot be determined.
        var spots6 = new List<(DateTimeOffset, decimal)>
        {
            (baseDate.AddMinutes(-15), 0.10m)
        };
        data.Add(
            "Insufficient History",
            baseDate,
            baseDate.AddMinutes(30),
            spots6,
            new List<(DateTimeOffset, DateTimeOffset)>()
        );

        return data;
    }
}
