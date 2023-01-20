using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using TeslaSolarCharger.Server.Dtos.Awattar;
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
    [InlineData(0.1631, 1674248400000, 1674252000000, "{\r\n      \"start_timestamp\": 1674248400000,\r\n      \"end_timestamp\": 1674252000000,\r\n      \"marketprice\": 163.1,\r\n      \"unit\": \"Eur/MWh\"\r\n    }")]
    [InlineData(0.147, 1674248400000, 1674252000000, "{\r\n      \"start_timestamp\": 1674248400000,\r\n      \"end_timestamp\": 1674252000000,\r\n      \"marketprice\": 147,\r\n      \"unit\": \"Eur/MWh\"\r\n    }")]
    [InlineData(-0.00517, 1674248400000, 1674252000000, "{\r\n      \"start_timestamp\": 1674248400000,\r\n      \"end_timestamp\": 1674252000000,\r\n      \"marketprice\": -5.17,\r\n      \"unit\": \"Eur/MWh\"\r\n    }")]
    [InlineData(0, 1674248400000, 1674252000000, "{\r\n      \"start_timestamp\": 1674248400000,\r\n      \"end_timestamp\": 1674252000000,\r\n      \"marketprice\": 0,\r\n      \"unit\": \"Eur/MWh\"\r\n    }")]
    public void Can_Generate_SpotPrice_From_AwattarPrice(decimal expectedPrice, long unixTimeStampStart, long unixTimeStampEnd, string valueJson)
    {
        var awattarDatum = JsonConvert.DeserializeObject<Datum>(valueJson);
        Assert.NotNull(awattarDatum);
        var spotPriceService = Mock.Create<TeslaSolarCharger.Server.Services.SpotPriceService>();
        var spotPrice = spotPriceService.GenerateSpotPriceFromAwattarPrice(awattarDatum);

        Assert.Equal(expectedPrice, spotPrice.Price);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(unixTimeStampStart), spotPrice.StartDate);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(unixTimeStampEnd), spotPrice.EndDate);
    }

    [Fact]
    public void Generates_Awattar_Url_With_DateTimeOffset()
    {
        var dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(1674511200);

        var spotPriceService = Mock.Create<TeslaSolarCharger.Server.Services.SpotPriceService>();
        var url = spotPriceService.GenerateAwattarUrl(dateTimeOffset);
        Assert.Equal("https://api.awattar.de/v1/marketdata?start=1674511200000&end=1674684000000", url);
    }

    [Fact]
    public void Generates_Awattar_Url_WithOut_DateTimeOffset()
    {
        var spotPriceService = Mock.Create<TeslaSolarCharger.Server.Services.SpotPriceService>();
        var url = spotPriceService.GenerateAwattarUrl(null);
        Assert.Equal("https://api.awattar.de/v1/marketdata", url);
    }
}
