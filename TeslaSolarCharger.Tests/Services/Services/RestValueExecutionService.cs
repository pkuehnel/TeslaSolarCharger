using TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;
using TeslaSolarCharger.SharedModel.Enums;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Services;

public class RestValueExecutionService(ITestOutputHelper outputHelper) : TestBase(outputHelper)
{
    [Fact]
    public void Can_Extract_Json_Value()
    {
        var service = Mock.Create<TeslaSolarCharger.Services.Services.RestValueExecutionService>();
        var json = "{\r\n  \"request\": {\r\n    \"method\": \"get\",\r\n    \"key\": \"asdf\"\r\n  },\r\n  \"code\": 0,\r\n  \"type\": \"call\",\r\n  \"data\": {\r\n    \"value\": 14\r\n  }\r\n}";
        var value = service.GetValue(json, NodePatternType.Json, new DtoRestValueResultConfiguration
        {
            Id = 1,
            NodePattern = "$.data.value",
        });
        Assert.Equal(14, value);
    }

    [Fact]
    public void Can_Extract_Xml_Value()
    {
        var service = Mock.Create<TeslaSolarCharger.Services.Services.RestValueExecutionService>();
        var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<Device Name=\"PIKO 4.6-2 MP plus\" Type=\"Inverter\" Platform=\"Net16\" HmiPlatform=\"HMI17\" NominalPower=\"4600\" UserPowerLimit=\"nan\" CountryPowerLimit=\"nan\" Serial=\"XXXXXXXXXXXXXXXXXXXX\" OEMSerial=\"XXXXXXXX\" BusAddress=\"1\" NetBiosName=\"XXXXXXXXXXXXXXX\" WebPortal=\"PIKO Solar Portal\" ManufacturerURL=\"kostal-solar-electric.com\" IpAddress=\"192.168.XXX.XXX\" DateTime=\"2022-06-08T19:33:25\" MilliSeconds=\"806\">\r\n  <Measurements>\r\n    <Measurement Value=\"231.3\" Unit=\"V\" Type=\"AC_Voltage\"/>\r\n    <Measurement Value=\"1.132\" Unit=\"A\" Type=\"AC_Current\"/>\r\n    <Measurement Value=\"256.1\" Unit=\"W\" Type=\"AC_Power\"/>\r\n    <Measurement Value=\"264.3\" Unit=\"W\" Type=\"AC_Power_fast\"/>\r\n    <Measurement Value=\"49.992\" Unit=\"Hz\" Type=\"AC_Frequency\"/>\r\n    <Measurement Value=\"474.2\" Unit=\"V\" Type=\"DC_Voltage\"/>\r\n    <Measurement Value=\"0.594\" Unit=\"A\" Type=\"DC_Current\"/>\r\n    <Measurement Value=\"473.5\" Unit=\"V\" Type=\"LINK_Voltage\"/>\r\n    <Measurement Value=\"18.7\" Unit=\"W\" Type=\"GridPower\"/>\r\n    <Measurement Value=\"0.0\" Unit=\"W\" Type=\"GridConsumedPower\"/>\r\n    <Measurement Value=\"18.7\" Unit=\"W\" Type=\"GridInjectedPower\"/>\r\n    <Measurement Value=\"237.3\" Unit=\"W\" Type=\"OwnConsumedPower\"/>\r\n    <Measurement Value=\"100.0\" Unit=\"%\" Type=\"Derating\"/>\r\n  </Measurements>\r\n</Device>";
        var value = service.GetValue(xml, NodePatternType.Xml, new DtoRestValueResultConfiguration
        {
            Id = 1,
            NodePattern = "Device/Measurements/Measurement",
            XmlAttributeHeaderName = "Type",
            XmlAttributeHeaderValue = "GridPower",
            XmlAttributeValueName = "Value",
        });
        Assert.Equal(18.7m, value);
    }

    [Fact]
    public void CanCalculateCorrectionFactor()
    {
        var service = Mock.Create<TeslaSolarCharger.Services.Services.RestValueExecutionService>();
        var value = service.MakeCalculationsOnRawValue(10, ValueOperator.Minus, 14);
        Assert.Equal(-140, value);
    }
}
