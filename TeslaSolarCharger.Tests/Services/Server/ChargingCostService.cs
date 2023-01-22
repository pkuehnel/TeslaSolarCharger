using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
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
        var spotPrices = await Context.SpotPrices.ToListAsync().ConfigureAwait(false);
        Assert.NotNull(spotPrices);
        Assert.Equal(1, spotPrices.Count);
    }
}
