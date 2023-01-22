using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

public class ChargingCostService : TestBase
{
    public ChargingCostService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public async Task Can_Load_SpotPrices()
    {
        Context.SpotPrices.AddRange(
            new List<SpotPrice>()
            {
                new SpotPrice()
                {
                    StartDate = new DateTime(2023, 1, 22, 17, 0, 0),
                    EndDate = new DateTime(2023, 1, 22, 18, 0, 0),
                    Price = new decimal(0.11)
                },
                new SpotPrice()
                {
                    StartDate = new DateTime(2023, 1, 22, 18, 0, 0),
                    EndDate = new DateTime(2023, 1, 22, 19, 0, 0),
                    Price = new decimal(0.11)
                },
                new SpotPrice()
                {
                    StartDate = new DateTime(2023, 1, 22, 19, 0, 0),
                    EndDate = new DateTime(2023, 1, 22, 20, 0, 0),
                    Price = new decimal(0.11)
                },
                new SpotPrice()
                {
                    StartDate = new DateTime(2023, 1, 22, 20, 0, 0),
                    EndDate = new DateTime(2023, 1, 22, 21, 0, 0),
                    Price = new decimal(0.11)
                },
            });
        await Context.SaveChangesAsync().ConfigureAwait(false);

        var spotPrices = await Context.SpotPrices.ToListAsync().ConfigureAwait(false);
        Assert.NotNull(spotPrices);
        Assert.Equal(4, spotPrices.Count);
    }

    [Fact]
    public async Task GetsSpotPricesInTimeSpan()
    {
        Context.SpotPrices.AddRange(
            new List<SpotPrice>()
            {
                new SpotPrice()
                {
                    StartDate = new DateTime(2023, 1, 22, 17, 0, 0),
                    EndDate = new DateTime(2023, 1, 22, 18, 0, 0),
                    Price = new decimal(0.11)
                },
                new SpotPrice()
                {
                    StartDate = new DateTime(2023, 1, 22, 18, 0, 0),
                    EndDate = new DateTime(2023, 1, 22, 19, 0, 0),
                    Price = new decimal(0.11)
                },
                new SpotPrice()
                {
                    StartDate = new DateTime(2023, 1, 22, 19, 0, 0),
                    EndDate = new DateTime(2023, 1, 22, 20, 0, 0),
                    Price = new decimal(0.11)
                },
                new SpotPrice()
                {
                    StartDate = new DateTime(2023, 1, 22, 20, 0, 0),
                    EndDate = new DateTime(2023, 1, 22, 21, 0, 0),
                    Price = new decimal(0.11)
                },
                new SpotPrice()
                {
                    StartDate = new DateTime(2023, 1, 22, 21, 0, 0),
                    EndDate = new DateTime(2023, 1, 22, 22, 0, 0),
                    Price = new decimal(0.11)
                },
            });
        await Context.SaveChangesAsync().ConfigureAwait(false);

        var startTime = new DateTime(2023, 1, 22, 18, 5, 0);
        var endTime = new DateTime(2023, 1, 22, 19, 8, 0);

        var chargingCostService = Mock.Create<TeslaSolarCharger.Server.Services.ChargingCostService>();
        var loadedSpotPrices = await chargingCostService.GetSpotPricesInTimeSpan(startTime, endTime).ConfigureAwait(false);
        Assert.Equal(2, loadedSpotPrices.Count);
    }
}
