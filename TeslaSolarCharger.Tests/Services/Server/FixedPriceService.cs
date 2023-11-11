using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using TeslaSolarCharger.Shared.Dtos.ChargingCost.CostConfigurations;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

public class FixedPriceService : TestBase
{
    public FixedPriceService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public void Can_Generate_Fixed_Price_Config()
    {
        var fixedPrices = new List<FixedPrice>()
        {
            new()
            {
                FromHour = 6,
                FromMinute = 0,
                ToHour = 15,
                ToMinute = 0,
                Value = 0.11m,
            },
            new()
            {
                FromHour = 15,
                FromMinute = 0,
                ToHour = 6,
                ToMinute = 0,
                Value = 0.30m,
            },
        };

        var fixedPriceService = Mock.Create<GridPriceProvider.Services.FixedPriceService>();
        var jsonString = fixedPriceService.GenerateConfigString(fixedPrices);
        var expectedJson = "[{\"FromHour\":6,\"FromMinute\":0,\"ToHour\":15,\"ToMinute\":0,\"Value\":0.11,\"ValidOnDays\":null},{\"FromHour\":15,\"FromMinute\":0,\"ToHour\":6,\"ToMinute\":0,\"Value\":0.30,\"ValidOnDays\":null}]";
        Assert.Equal(expectedJson, jsonString);
    }

    [Fact]
    public void Can_Generate_Prices_Based_On_Fixed_Prices()
    {
        var fixedPrices = new List<FixedPrice>()
        {
            new()
            {
                FromHour = 6,
                FromMinute = 0,
                ToHour = 15,
                ToMinute = 0,
                Value = 0.11m,
            },
            new()
            {
                FromHour = 15,
                FromMinute = 0,
                ToHour = 6,
                ToMinute = 0,
                Value = 0.30m,
            },
        };
        var fixedPriceService = Mock.Create<GridPriceProvider.Services.FixedPriceService>();
        var prices = fixedPriceService.GeneratePricesBasedOnFixedPrices(new DateTimeOffset(2023, 1, 21, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2023, 1, 29, 0, 0, 0, TimeSpan.Zero), fixedPrices);
    }

    [Fact]
    public void Can_Split_Fixed_Prices_On_Midnight_Weekdays_Null()
    {
        var fixedPrices = new List<FixedPrice>()
        {
            new()
            {
                FromHour = 6,
                FromMinute = 0,
                ToHour = 15,
                ToMinute = 0,
                Value = 0.11m,
            },
            new()
            {
                FromHour = 15,
                FromMinute = 0,
                ToHour = 6,
                ToMinute = 0,
                Value = 0.30m,
            },
        };
        var fixedPriceService = Mock.Create<GridPriceProvider.Services.FixedPriceService>();
        var midnightSeparatedFixedPrices = fixedPriceService.SplitFixedPricesAtMidnight(fixedPrices);
        Assert.Equal(3, midnightSeparatedFixedPrices.Count);
        Assert.Single(midnightSeparatedFixedPrices.Where(p => p is { FromHour: 6, FromMinute: 0, ToHour: 15, ToMinute: 0, Value: 0.11m, ValidOnDays: null}));
        Assert.Single(midnightSeparatedFixedPrices.Where(p => p is { FromHour: 0, FromMinute: 0, ToHour: 6, ToMinute: 0, Value: 0.30m, ValidOnDays: null }));
        Assert.Single(midnightSeparatedFixedPrices.Where(p => p is { FromHour: 15, FromMinute: 0, ToHour: 0, ToMinute: 0, Value: 0.30m, ValidOnDays: null }));
    }

    [Fact]
    public void Can_Split_Fixed_Prices_On_Midnight_Weekdays_Not_Null()
    {
        var fixedPrices = new List<FixedPrice>()
        {
            new()
            {
                FromHour = 7,
                FromMinute = 0,
                ToHour = 20,
                ToMinute = 0,
                Value = 0.2462m,
                ValidOnDays = new List<DayOfWeek>() { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
            },
            new()
            {
                FromHour = 7,
                FromMinute = 0,
                ToHour = 13,
                ToMinute = 0,
                Value = 0.2462m,
                ValidOnDays = new List<DayOfWeek>() { DayOfWeek.Saturday },
            },
            new()
            {
                FromHour = 20,
                FromMinute = 0,
                ToHour = 7,
                ToMinute = 0,
                Value = 0.2134m,
                ValidOnDays = new List<DayOfWeek>() { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
            },
            new()
            {
                FromHour = 13,
                FromMinute = 0,
                ToHour = 7,
                ToMinute = 0,
                Value = 0.2134m,
                ValidOnDays = new List<DayOfWeek>() { DayOfWeek.Saturday },
            },
            new()
            {
                FromHour = 0,
                FromMinute = 0,
                ToHour = 0,
                ToMinute = 0,
                Value = 0.2134m,
                ValidOnDays = new List<DayOfWeek>() { DayOfWeek.Sunday },
            },
        };
        var fixedPriceService = Mock.Create<GridPriceProvider.Services.FixedPriceService>();
        var midnightSeparatedFixedPrices = fixedPriceService.SplitFixedPricesAtMidnight(fixedPrices);
        Assert.Equal(7, midnightSeparatedFixedPrices.Count);
        Assert.Single(midnightSeparatedFixedPrices.Where(p => p is { FromHour: 0, FromMinute: 0, ToHour: 7, ToMinute: 0, Value: 0.2134m, ValidOnDays.Count: 5 }
                                                              && p.ValidOnDays.Contains(DayOfWeek.Monday)
                                                              && p.ValidOnDays.Contains(DayOfWeek.Tuesday)
                                                              && p.ValidOnDays.Contains(DayOfWeek.Wednesday)
                                                              && p.ValidOnDays.Contains(DayOfWeek.Thursday)
                                                              && p.ValidOnDays.Contains(DayOfWeek.Friday)));
        Assert.Single(midnightSeparatedFixedPrices.Where(p => p is { FromHour: 7, FromMinute: 0, ToHour: 20, ToMinute: 0, Value: 0.2462m, ValidOnDays.Count: 5 } 
                                                              && p.ValidOnDays.Contains(DayOfWeek.Monday) 
                                                              && p.ValidOnDays.Contains(DayOfWeek.Tuesday) 
                                                              && p.ValidOnDays.Contains(DayOfWeek.Wednesday) 
                                                              && p.ValidOnDays.Contains(DayOfWeek.Thursday) 
                                                              && p.ValidOnDays.Contains(DayOfWeek.Friday)));
        Assert.Single(midnightSeparatedFixedPrices.Where(p => p is { FromHour: 20, FromMinute: 0, ToHour: 0, ToMinute: 0, Value: 0.2134m, ValidOnDays.Count: 5 }
                                                              && p.ValidOnDays.Contains(DayOfWeek.Monday)
                                                              && p.ValidOnDays.Contains(DayOfWeek.Tuesday)
                                                              && p.ValidOnDays.Contains(DayOfWeek.Wednesday)
                                                              && p.ValidOnDays.Contains(DayOfWeek.Thursday)
                                                              && p.ValidOnDays.Contains(DayOfWeek.Friday)));
        Assert.Single(midnightSeparatedFixedPrices.Where(p => p is { FromHour: 0, FromMinute: 0, ToHour: 7, ToMinute: 0, Value: 0.2134m, ValidOnDays.Count: 1 }
                                                              && p.ValidOnDays.Contains(DayOfWeek.Saturday)));
        Assert.Single(midnightSeparatedFixedPrices.Where(p => p is { FromHour: 7, FromMinute: 0, ToHour: 13, ToMinute: 0, Value: 0.2462m, ValidOnDays.Count: 1 }
                                                              && p.ValidOnDays.Contains(DayOfWeek.Saturday)));
        Assert.Single(midnightSeparatedFixedPrices.Where(p => p is { FromHour: 13, FromMinute: 0, ToHour: 0, ToMinute: 0, Value: 0.2134m, ValidOnDays.Count: 1 }
                                                              && p.ValidOnDays.Contains(DayOfWeek.Saturday)));
        Assert.Single(midnightSeparatedFixedPrices.Where(p => p is { FromHour: 0, FromMinute: 0, ToHour: 0, ToMinute: 0, Value: 0.2134m, ValidOnDays.Count: 1 }
                                                              && p.ValidOnDays.Contains(DayOfWeek.Sunday)));
    }
}
