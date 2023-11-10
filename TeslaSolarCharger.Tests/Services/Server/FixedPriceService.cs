using System;
using System.Collections.Generic;
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

        var chargingCostService = Mock.Create<GridPriceProvider.Services.FixedPriceService>();
        var jsonString = chargingCostService.GenerateConfigString(fixedPrices);
        var expectedJson = "[{\"FromHour\":6,\"FromMinute\":0,\"ToHour\":15,\"ToMinute\":0,\"Value\":0.11},{\"FromHour\":15,\"FromMinute\":0,\"ToHour\":6,\"ToMinute\":0,\"Value\":0.30}]";
        Assert.Equal(expectedJson, jsonString);
    }
}
