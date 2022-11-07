using System.Net.Http;
using TeslaSolarCharger.Server.Enums;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Enums;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

public class PvValueService : TestBase
{
    public PvValueService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }


    [Theory]
    [InlineData("384")]
    [InlineData("384.0")]
    [InlineData("384.00")]
    public void Can_extract_Integers_From_String(string value)
    {
        var pvValueService = Mock.Create<TeslaSolarCharger.Server.Services.PvValueService>();
        var intValue = pvValueService.GetdoubleFromStringResult(value);

        Assert.Equal(384, intValue);
    }

    [Theory]
    [InlineData("384")]
    [InlineData("384.0")]
    [InlineData("384.00")]
    public void Can_Get_Integer_From_Plain_Result(string text)
    {
        var pvValueService = Mock.Create<TeslaSolarCharger.Server.Services.PvValueService>();
        var intValue = pvValueService.GetValueFromResult("", text, NodePatternType.Direct, true);

        Assert.Equal(384, intValue);
    }

    [Theory]
    [InlineData("384")]
    [InlineData("384.0")]
    [InlineData("384.00")]
    public void Can_Get_Integer_From_Json_Result(string text)
    {
        var json = string.Format(
            "{{\"request\": {{\"method\": \"get\", \"key\": \"CO@13_3_0\"}}, \"code\": 0, \"type\": \"call\", \"data\": {{\"value\": {0}}}}}", text);
        var pvValueService = Mock.Create<TeslaSolarCharger.Server.Services.PvValueService>();
        var intValue = pvValueService.GetValueFromResult("$.data.value", json, NodePatternType.Json, true);

        Assert.Equal(384, intValue);
    }

    [Theory]
    [InlineData("384")]
    [InlineData("384.0")]
    [InlineData("384.00")]
    public void Can_Get_Integer_From_Simple_Result(string text)
    {
        var json = $"{{\"value\": {text}}}";
        var pvValueService = Mock.Create<TeslaSolarCharger.Server.Services.PvValueService>();
        var intValue = pvValueService.GetValueFromResult("$.value", json, NodePatternType.Json, true);

        Assert.Equal(384, intValue);
    }

    [Theory]
    [InlineData("384")]
    [InlineData("384.0")]
    [InlineData("384.00")]
    public void Can_Get_Integer_From_Grid_Xml_Attribute_Result(string text)
    {
        var xml = string.Format(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<Device Name=\"PIKO 4.6-2 MP plus\" Type=\"Inverter\" Platform=\"Net16\" HmiPlatform=\"HMI17\" NominalPower=\"4600\" UserPowerLimit=\"nan\" CountryPowerLimit=\"nan\" Serial=\"XXXXXXXXXXXXXXXXXXXX\" OEMSerial=\"XXXXXXXX\" BusAddress=\"1\" NetBiosName=\"XXXXXXXXXXXXXXX\" WebPortal=\"PIKO Solar Portal\" ManufacturerURL=\"kostal-solar-electric.com\" IpAddress=\"192.168.XXX.XXX\" DateTime=\"2022-06-08T19:33:25\" MilliSeconds=\"806\">\r\n  <Measurements>\r\n    <Measurement Value=\"231.3\" Unit=\"V\" Type=\"AC_Voltage\"/>\r\n    <Measurement Value=\"1.132\" Unit=\"A\" Type=\"AC_Current\"/>\r\n    <Measurement Value=\"256.1\" Unit=\"W\" Type=\"AC_Power\"/>\r\n    <Measurement Value=\"264.3\" Unit=\"W\" Type=\"AC_Power_fast\"/>\r\n    <Measurement Value=\"49.992\" Unit=\"Hz\" Type=\"AC_Frequency\"/>\r\n    <Measurement Value=\"474.2\" Unit=\"V\" Type=\"DC_Voltage\"/>\r\n    <Measurement Value=\"0.594\" Unit=\"A\" Type=\"DC_Current\"/>\r\n    <Measurement Value=\"473.5\" Unit=\"V\" Type=\"LINK_Voltage\"/>\r\n    <Measurement Value=\"{0}\" Unit=\"W\" Type=\"GridPower\"/>\r\n    <Measurement Value=\"0.0\" Unit=\"W\" Type=\"GridConsumedPower\"/>\r\n    <Measurement Value=\"18.7\" Unit=\"W\" Type=\"GridInjectedPower\"/>\r\n    <Measurement Value=\"237.3\" Unit=\"W\" Type=\"OwnConsumedPower\"/>\r\n    <Measurement Value=\"100.0\" Unit=\"%\" Type=\"Derating\"/>\r\n  </Measurements>\r\n</Device>", text);
        var pvValueService = Mock.Create<TeslaSolarCharger.Server.Services.PvValueService>();
        Mock.Mock<IConfigurationWrapper>().Setup(s => s.CurrentPowerToGridXmlAttributeHeaderName()).Returns("Type");
        Mock.Mock<IConfigurationWrapper>().Setup(s => s.CurrentPowerToGridXmlAttributeHeaderValue()).Returns("GridPower");
        Mock.Mock<IConfigurationWrapper>().Setup(s => s.CurrentPowerToGridXmlAttributeValueName()).Returns("Value");

        var intValue = pvValueService.GetValueFromResult("Device/Measurements/Measurement", xml, NodePatternType.Xml, true);

        Assert.Equal(384, intValue);
    }

    [Theory]
    [InlineData("384")]
    [InlineData("384.0")]
    [InlineData("384.00")]
    public void Can_Get_Integer_From_Inverter_Xml_Attribute_Result(string text)
    {
        var xml = string.Format(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<Device Name=\"PIKO 4.6-2 MP plus\" Type=\"Inverter\" Platform=\"Net16\" HmiPlatform=\"HMI17\" NominalPower=\"4600\" UserPowerLimit=\"nan\" CountryPowerLimit=\"nan\" Serial=\"XXXXXXXXXXXXXXXXXXXX\" OEMSerial=\"XXXXXXXX\" BusAddress=\"1\" NetBiosName=\"XXXXXXXXXXXXXXX\" WebPortal=\"PIKO Solar Portal\" ManufacturerURL=\"kostal-solar-electric.com\" IpAddress=\"192.168.XXX.XXX\" DateTime=\"2022-06-08T19:33:25\" MilliSeconds=\"806\">\r\n  <Measurements>\r\n    <Measurement Value=\"231.3\" Unit=\"V\" Type=\"AC_Voltage\"/>\r\n    <Measurement Value=\"1.132\" Unit=\"A\" Type=\"AC_Current\"/>\r\n    <Measurement Value=\"{0}\" Unit=\"W\" Type=\"AC_Power\"/>\r\n    <Measurement Value=\"264.3\" Unit=\"W\" Type=\"AC_Power_fast\"/>\r\n    <Measurement Value=\"49.992\" Unit=\"Hz\" Type=\"AC_Frequency\"/>\r\n    <Measurement Value=\"474.2\" Unit=\"V\" Type=\"DC_Voltage\"/>\r\n    <Measurement Value=\"0.594\" Unit=\"A\" Type=\"DC_Current\"/>\r\n    <Measurement Value=\"473.5\" Unit=\"V\" Type=\"LINK_Voltage\"/>\r\n    <Measurement Value=\"18.7\" Unit=\"W\" Type=\"GridPower\"/>\r\n    <Measurement Value=\"0.0\" Unit=\"W\" Type=\"GridConsumedPower\"/>\r\n    <Measurement Value=\"18.7\" Unit=\"W\" Type=\"GridInjectedPower\"/>\r\n    <Measurement Value=\"237.3\" Unit=\"W\" Type=\"OwnConsumedPower\"/>\r\n    <Measurement Value=\"100.0\" Unit=\"%\" Type=\"Derating\"/>\r\n  </Measurements>\r\n</Device>", text);
        var pvValueService = Mock.Create<TeslaSolarCharger.Server.Services.PvValueService>();
        Mock.Mock<IConfigurationWrapper>().Setup(s => s.CurrentInverterPowerXmlAttributeHeaderName()).Returns("Type");
        Mock.Mock<IConfigurationWrapper>().Setup(s => s.CurrentInverterPowerXmlAttributeHeaderValue()).Returns("AC_Power");
        Mock.Mock<IConfigurationWrapper>().Setup(s => s.CurrentInverterPowerXmlAttributeValueName()).Returns("Value");

        var intValue = pvValueService.GetValueFromResult("Device/Measurements/Measurement", xml, NodePatternType.Xml, false);

        Assert.Equal(384, intValue);
    }

    [Theory]
    [InlineData("384")]
    [InlineData("384.0")]
    [InlineData("384.00")]
    public void Can_Get_Integer_From_Xml_Node_Result(string text)
    {
        var xml = string.Format(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<Device Name=\"PIKO 4.6-2 MP plus\" Type=\"Inverter\" Platform=\"Net16\" HmiPlatform=\"HMI17\" NominalPower=\"4600\" UserPowerLimit=\"nan\" CountryPowerLimit=\"nan\" Serial=\"XXXXXXXXXXXXXXXXXXXX\" OEMSerial=\"XXXXXXXX\" BusAddress=\"1\" NetBiosName=\"XXXXXXXXXXXXXXX\" WebPortal=\"PIKO Solar Portal\" ManufacturerURL=\"kostal-solar-electric.com\" IpAddress=\"192.168.XXX.XXX\" DateTime=\"2022-06-08T19:33:25\" MilliSeconds=\"806\">\r\n  <Measurements>\r\n    <InverterPower>1000</InverterPower>\r\n    <GridPower>{0}</GridPower>\r\n  </Measurements>\r\n</Device>", text);
        var pvValueService = Mock.Create<TeslaSolarCharger.Server.Services.PvValueService>();

        var intValue = pvValueService.GetValueFromResult("Device/Measurements/GridPower", xml, NodePatternType.Xml, false);

        Assert.Equal(384, intValue);
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
