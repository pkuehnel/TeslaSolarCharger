using System.Net.Http;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

public class PvValueService : TestBase
{
    public PvValueService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public void Detects_Identical_HttpRequests()
    {
        var request1 = new HttpRequestMessage(HttpMethod.Get, "http://192.168.1.50:5007/api/ChargingLog/GetAverageGridPowerOfLastXseconds");
        request1.Headers.Add("Accept", "*/*");
        request1.Headers.Add("Key2", "Value2");

        var request2 = new HttpRequestMessage(HttpMethod.Get, "http://192.168.1.50:5007/api/ChargingLog/GetAverageGridPowerOfLastXseconds");
        request2.Headers.Add("Key2", "Value2");
        request2.Headers.Add("Accept", "*/*");

        var pvValueService = Mock.Create<TeslaSolarCharger.Server.Services.PvValueService>();

        Assert.True(pvValueService.IsSameRequest(request1, request2));
    }

    [Fact]
    public void Detects_Different_Urls()
    {
        var request1 = new HttpRequestMessage(HttpMethod.Get, "http://192.168.1.51:5007/api/ChargingLog/GetAverageGridPowerOfLastXseconds");
        request1.Headers.Add("Accept", "*/*");
        request1.Headers.Add("Key2", "Value2");

        var request2 = new HttpRequestMessage(HttpMethod.Get, "http://192.168.1.50:5007/api/ChargingLog/GetAverageGridPowerOfLastXseconds");
        request2.Headers.Add("Key2", "Value2");
        request2.Headers.Add("Accept", "*/*");

        var pvValueService = Mock.Create<TeslaSolarCharger.Server.Services.PvValueService>();

        Assert.False(pvValueService.IsSameRequest(request1, request2));
    }

    [Fact]
    public void Detects_Different_Methods()
    {
        var request1 = new HttpRequestMessage(HttpMethod.Post, "http://192.168.1.50:5007/api/ChargingLog/GetAverageGridPowerOfLastXseconds");
        request1.Headers.Add("Accept", "*/*");
        request1.Headers.Add("Key2", "Value2");

        var request2 = new HttpRequestMessage(HttpMethod.Get, "http://192.168.1.50:5007/api/ChargingLog/GetAverageGridPowerOfLastXseconds");
        request2.Headers.Add("Key2", "Value2");
        request2.Headers.Add("Accept", "*/*");

        var pvValueService = Mock.Create<TeslaSolarCharger.Server.Services.PvValueService>();

        Assert.False(pvValueService.IsSameRequest(request1, request2));
    }

    [Fact]
    public void Detects_Different_Header_Keys()
    {
        var request1 = new HttpRequestMessage(HttpMethod.Get, "http://192.168.1.50:5007/api/ChargingLog/GetAverageGridPowerOfLastXseconds");
        request1.Headers.Add("Accept", "*/*");
        request1.Headers.Add("Key1", "Value2");

        var request2 = new HttpRequestMessage(HttpMethod.Get, "http://192.168.1.50:5007/api/ChargingLog/GetAverageGridPowerOfLastXseconds");
        request2.Headers.Add("Key2", "Value2");
        request2.Headers.Add("Accept", "*/*");

        var pvValueService = Mock.Create<TeslaSolarCharger.Server.Services.PvValueService>();

        Assert.False(pvValueService.IsSameRequest(request1, request2));
    }

    [Fact]
    public void Detects_Different_Header_Values()
    {
        var request1 = new HttpRequestMessage(HttpMethod.Get, "http://192.168.1.50:5007/api/ChargingLog/GetAverageGridPowerOfLastXseconds");
        request1.Headers.Add("Accept", "*/*");
        request1.Headers.Add("Key2", "Value1");

        var request2 = new HttpRequestMessage(HttpMethod.Get, "http://192.168.1.50:5007/api/ChargingLog/GetAverageGridPowerOfLastXseconds");
        request2.Headers.Add("Key2", "Value2");
        request2.Headers.Add("Accept", "*/*");

        var pvValueService = Mock.Create<TeslaSolarCharger.Server.Services.PvValueService>();

        Assert.False(pvValueService.IsSameRequest(request1, request2));
    }

    [Fact]
    public void Detects_Missing_Headers()
    {
        var request1 = new HttpRequestMessage(HttpMethod.Get, "http://192.168.1.50:5007/api/ChargingLog/GetAverageGridPowerOfLastXseconds");
        request1.Headers.Add("Accept", "*/*");

        var request2 = new HttpRequestMessage(HttpMethod.Get, "http://192.168.1.50:5007/api/ChargingLog/GetAverageGridPowerOfLastXseconds");
        request2.Headers.Add("Key2", "Value2");
        request2.Headers.Add("Accept", "*/*");

        var pvValueService = Mock.Create<TeslaSolarCharger.Server.Services.PvValueService>();

        Assert.False(pvValueService.IsSameRequest(request1, request2));
    }

    [Fact]
    public void Detects_To_Many_Headers()
    {
        var request1 = new HttpRequestMessage(HttpMethod.Get, "http://192.168.1.50:5007/api/ChargingLog/GetAverageGridPowerOfLastXseconds");
        request1.Headers.Add("Accept", "*/*");
        request1.Headers.Add("Key2", "Value2");

        var request2 = new HttpRequestMessage(HttpMethod.Get, "http://192.168.1.50:5007/api/ChargingLog/GetAverageGridPowerOfLastXseconds");
        request2.Headers.Add("Accept", "*/*");

        var pvValueService = Mock.Create<TeslaSolarCharger.Server.Services.PvValueService>();

        Assert.False(pvValueService.IsSameRequest(request1, request2));
    }
}
