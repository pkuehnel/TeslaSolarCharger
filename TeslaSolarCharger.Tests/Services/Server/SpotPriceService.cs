using System;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Enums;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

public class SpotPriceService : TestBase
{
    public SpotPriceService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Theory]
    [InlineData(0.07996, 1761433200, 0,  "{\r\n  \"license_info\": \"asdf\",\r\n  \"unix_seconds\": [\r\n    1761433200,\r\n    1761434100,\r\n    1761435000,\r\n    1761435900,\r\n    1761436800,\r\n    1761437700,\r\n    1761438600,\r\n    1761439500,\r\n    1761440400\r\n  ],\r\n  \"price\": [\r\n    4.01,\r\n    3.76,\r\n    3.75,\r\n    3.27,\r\n    3.99,\r\n    3.33,\r\n    3.04,\r\n    2.4,\r\n    2.89\r\n  ],\r\n  \"unit\": \"EUR / MWh\",\r\n  \"deprecated\": false\r\n}")]
    [InlineData(0.0766, 1761434100, 1,  "{\r\n  \"license_info\": \"asdf\",\r\n  \"unix_seconds\": [\r\n    1761433200,\r\n    1761434100,\r\n    1761435000,\r\n    1761435900,\r\n    1761436800,\r\n    1761437700,\r\n    1761438600,\r\n    1761439500,\r\n    1761440400\r\n  ],\r\n  \"price\": [\r\n    4.01,\r\n    3.76,\r\n    3.75,\r\n    3.27,\r\n    3.99,\r\n    3.33,\r\n    3.04,\r\n    2.4,\r\n    2.89\r\n  ],\r\n  \"unit\": \"EUR / MWh\",\r\n  \"deprecated\": false\r\n}")]
    [InlineData(0.06322, 1761440400, 8,  "{\r\n  \"license_info\": \"asdf\",\r\n  \"unix_seconds\": [\r\n    1761433200,\r\n    1761434100,\r\n    1761435000,\r\n    1761435900,\r\n    1761436800,\r\n    1761437700,\r\n    1761438600,\r\n    1761439500,\r\n    1761440400\r\n  ],\r\n  \"price\": [\r\n    4.01,\r\n    3.76,\r\n    3.75,\r\n    3.27,\r\n    3.99,\r\n    3.33,\r\n    3.04,\r\n    2.4,\r\n    2.89\r\n  ],\r\n  \"unit\": \"EUR / MWh\",\r\n  \"deprecated\": false\r\n}")]
    public void Can_Generate_PriceFromJson(decimal expectedPrice, long unixTimeStampStart, int index, string json)
    {
        var spotPriceService = Mock.Create<TeslaSolarCharger.Server.Services.SpotPriceService>();
        var spotPrice = spotPriceService.GetPrices(json);
        Assert.NotNull(spotPrice);
        Assert.Equal(expectedPrice, spotPrice.price[index]);
        Assert.Equal(unixTimeStampStart, spotPrice.unix_seconds[index]);
    }

    [Theory]
    [InlineData(true, 0.07996, 1761433200, "{\r\n  \"license_info\": \"asdf\",\r\n  \"unix_seconds\": [\r\n    1761433200,\r\n    1761434100,\r\n    1761435000,\r\n    1761435900,\r\n    1761436800,\r\n    1761437700,\r\n    1761438600,\r\n    1761439500,\r\n    1761440400\r\n  ],\r\n  \"price\": [\r\n    4.01,\r\n    3.76,\r\n    3.75,\r\n    3.27,\r\n    3.99,\r\n    3.33,\r\n    3.04,\r\n    2.4,\r\n    2.89\r\n  ],\r\n  \"unit\": \"EUR / MWh\",\r\n  \"deprecated\": false\r\n}")]
    [InlineData(false, 0.07996, 1761433200, "{\r\n  \"license_info\": \"asdf\",\r\n  \"unix_seconds\": [\r\n    1761433200,\r\n    1761434100,\r\n    1761435000,\r\n    1761435900,\r\n    1761436800,\r\n    1761437700,\r\n    1761438600,\r\n    1761439500,\r\n    1761440400\r\n  ],\r\n  \"price\": [\r\n    4.01,\r\n    3.76,\r\n    3.75,\r\n    3.27,\r\n    3.99,\r\n    3.33,\r\n    3.04,\r\n    2.4,\r\n    2.89\r\n  ],\r\n  \"unit\": \"EUR / MWh\",\r\n  \"deprecated\": false\r\n}")]
    [InlineData(true, 0.0766, 1761434100, "{\r\n  \"license_info\": \"asdf\",\r\n  \"unix_seconds\": [\r\n    1761433200,\r\n    1761434100,\r\n    1761435000,\r\n    1761435900,\r\n    1761436800,\r\n    1761437700,\r\n    1761438600,\r\n    1761439500,\r\n    1761440400\r\n  ],\r\n  \"price\": [\r\n    4.01,\r\n    3.76,\r\n    3.75,\r\n    3.27,\r\n    3.99,\r\n    3.33,\r\n    3.04,\r\n    2.4,\r\n    2.89\r\n  ],\r\n  \"unit\": \"EUR / MWh\",\r\n  \"deprecated\": false\r\n}")]
    [InlineData(false, 0.0766, 1761434100, "{\r\n  \"license_info\": \"asdf\",\r\n  \"unix_seconds\": [\r\n    1761433200,\r\n    1761434100,\r\n    1761435000,\r\n    1761435900,\r\n    1761436800,\r\n    1761437700,\r\n    1761438600,\r\n    1761439500,\r\n    1761440400\r\n  ],\r\n  \"price\": [\r\n    4.01,\r\n    3.76,\r\n    3.75,\r\n    3.27,\r\n    3.99,\r\n    3.33,\r\n    3.04,\r\n    2.4,\r\n    2.89\r\n  ],\r\n  \"unit\": \"EUR / MWh\",\r\n  \"deprecated\": false\r\n}")]
    [InlineData(true, 0.06322, 1761440400, "{\r\n  \"license_info\": \"asdf\",\r\n  \"unix_seconds\": [\r\n    1761433200,\r\n    1761434100,\r\n    1761435000,\r\n    1761435900,\r\n    1761436800,\r\n    1761437700,\r\n    1761438600,\r\n    1761439500,\r\n    1761440400\r\n  ],\r\n  \"price\": [\r\n    4.01,\r\n    3.76,\r\n    3.75,\r\n    3.27,\r\n    3.99,\r\n    3.33,\r\n    3.04,\r\n    2.4,\r\n    2.89\r\n  ],\r\n  \"unit\": \"EUR / MWh\",\r\n  \"deprecated\": false\r\n}")]
    [InlineData(false, 0.06322, 1761440400, "{\r\n  \"license_info\": \"asdf\",\r\n  \"unix_seconds\": [\r\n    1761433200,\r\n    1761434100,\r\n    1761435000,\r\n    1761435900,\r\n    1761436800,\r\n    1761437700,\r\n    1761438600,\r\n    1761439500,\r\n    1761440400\r\n  ],\r\n  \"price\": [\r\n    4.01,\r\n    3.76,\r\n    3.75,\r\n    3.27,\r\n    3.99,\r\n    3.33,\r\n    3.04,\r\n    2.4,\r\n    2.89\r\n  ],\r\n  \"unit\": \"EUR / MWh\",\r\n  \"deprecated\": false\r\n}")]
    public void Can_Generate_SpotPrice_From_AwattarPrice(bool shouldAddTick, decimal expectedPrice, long unixTimeStampStart, string json)
    {
        var spotPriceService = Mock.Create<TeslaSolarCharger.Server.Services.SpotPriceService>();
        var energyChartPrices = spotPriceService.GetPrices(json);
        Assert.NotNull(energyChartPrices);
        var startDate = DateTimeOffset.FromUnixTimeSeconds(unixTimeStampStart);
        if (shouldAddTick)
        {
            startDate = startDate.AddTicks(1);
        }
        var region = SpotPriceRegion.BE;
        var spotPrices = spotPriceService.GenerateSpotPricesFromEnergyChartPrices(startDate, energyChartPrices, region);
        Assert.NotNull(spotPrices);
        var counter = 0;
        foreach (var spotPrice in spotPrices)
        {
            if (DateTimeOffset.FromUnixTimeSeconds(unixTimeStampStart) == new DateTimeOffset(spotPrice.StartDate, TimeSpan.Zero))
            {
                counter++;
                Assert.Equal(expectedPrice, spotPrice.Price);
            }
        }
        Assert.Equal(unixTimeStampStart > 1761433200 || !shouldAddTick ? 0 : 1, counter);
    }

    [Fact]
    public void Generates_Awattar_Url_With_DateTimeOffset()
    {
        var currentDate = new DateTimeOffset(2023, 3, 1, 10, 0, 0, TimeSpan.Zero);
        Mock.Mock<IDateTimeProvider>()
            .Setup(d => d.DateTimeOffSetNow())
            .Returns(currentDate);
        Mock.Mock<IConfigurationWrapper>()
            .Setup(c => c.GetAwattarBaseUrl())
            .Returns("https://api.awattar.de/v1/marketdata");

        var startDate = new DateTimeOffset(2025, 10, 25, 23, 0, 0, TimeSpan.Zero);
        var endDate = new DateTimeOffset(2025, 10, 26, 1, 0, 0, TimeSpan.Zero);

        var spotPriceService = Mock.Create<TeslaSolarCharger.Server.Services.SpotPriceService>();
        var url = spotPriceService.GenerateEnergyChartUrl(startDate, endDate, SpotPriceRegion.DE_LU.ToRegionCode());
        Assert.Equal("https://api.energy-charts.info/price?bzn=DE-LU&start=2025-10-25T23%3A00Z&end=2025-10-26T01%3A00Z", url);
    }
}
