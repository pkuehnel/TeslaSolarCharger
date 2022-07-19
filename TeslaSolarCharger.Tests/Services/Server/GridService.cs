using TeslaSolarCharger.Server.Enums;
using TeslaSolarCharger.Shared.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

public class GridService : TestBase
{
    public GridService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Theory]
    [InlineData("384.8746")]
    [InlineData("384")]
    [InlineData("384.0")]
    [InlineData("384.147")]
    public void Can_extract_Integers_From_String(string value)
    {
        var gridService = Mock.Create<TeslaSolarCharger.Server.Services.GridService>();
        var intValue = gridService.GetIntegerFromString(value);

        Assert.Equal(384, intValue);
    }

    [Theory]
    [InlineData("384.8746")]
    [InlineData("384")]
    [InlineData("384.0")]
    [InlineData("384.147")]
    public void Can_Get_Integer_From_Plain_Result(string text)
    {
        var gridService = Mock.Create<TeslaSolarCharger.Server.Services.GridService>();
        var intValue = gridService.GetValueFromResult("", text, NodePatternType.None, true);

        Assert.Equal(384, intValue);
    }

    [Theory]
    [InlineData("384.8746")]
    [InlineData("384")]
    [InlineData("384.0")]
    [InlineData("384.147")]
    public void Can_Get_Integer_From_Json_Result(string text)
    {
        var json = string.Format(
            "{{\"request\": {{\"method\": \"get\", \"key\": \"CO@13_3_0\"}}, \"code\": 0, \"type\": \"call\", \"data\": {{\"value\": {0}}}}}", text);
        var gridService = Mock.Create<TeslaSolarCharger.Server.Services.GridService>();
        var intValue = gridService.GetValueFromResult("$.data.value", json, NodePatternType.Json, true);

        Assert.Equal(384, intValue);
    }

    [Theory]
    [InlineData("384.8746")]
    [InlineData("384")]
    [InlineData("384.0")]
    [InlineData("384.147")]
    public void Can_Get_Integer_From_Grid_Xml_Attribute_Result(string text)
    {
        var xml = string.Format(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<Device Name=\"PIKO 4.6-2 MP plus\" Type=\"Inverter\" Platform=\"Net16\" HmiPlatform=\"HMI17\" NominalPower=\"4600\" UserPowerLimit=\"nan\" CountryPowerLimit=\"nan\" Serial=\"XXXXXXXXXXXXXXXXXXXX\" OEMSerial=\"XXXXXXXX\" BusAddress=\"1\" NetBiosName=\"XXXXXXXXXXXXXXX\" WebPortal=\"PIKO Solar Portal\" ManufacturerURL=\"kostal-solar-electric.com\" IpAddress=\"192.168.XXX.XXX\" DateTime=\"2022-06-08T19:33:25\" MilliSeconds=\"806\">\r\n  <Measurements>\r\n    <Measurement Value=\"231.3\" Unit=\"V\" Type=\"AC_Voltage\"/>\r\n    <Measurement Value=\"1.132\" Unit=\"A\" Type=\"AC_Current\"/>\r\n    <Measurement Value=\"256.1\" Unit=\"W\" Type=\"AC_Power\"/>\r\n    <Measurement Value=\"264.3\" Unit=\"W\" Type=\"AC_Power_fast\"/>\r\n    <Measurement Value=\"49.992\" Unit=\"Hz\" Type=\"AC_Frequency\"/>\r\n    <Measurement Value=\"474.2\" Unit=\"V\" Type=\"DC_Voltage\"/>\r\n    <Measurement Value=\"0.594\" Unit=\"A\" Type=\"DC_Current\"/>\r\n    <Measurement Value=\"473.5\" Unit=\"V\" Type=\"LINK_Voltage\"/>\r\n    <Measurement Value=\"{0}\" Unit=\"W\" Type=\"GridPower\"/>\r\n    <Measurement Value=\"0.0\" Unit=\"W\" Type=\"GridConsumedPower\"/>\r\n    <Measurement Value=\"18.7\" Unit=\"W\" Type=\"GridInjectedPower\"/>\r\n    <Measurement Value=\"237.3\" Unit=\"W\" Type=\"OwnConsumedPower\"/>\r\n    <Measurement Value=\"100.0\" Unit=\"%\" Type=\"Derating\"/>\r\n  </Measurements>\r\n</Device>", text);
        var gridService = Mock.Create<TeslaSolarCharger.Server.Services.GridService>();
        Mock.Mock<IConfigurationWrapper>().Setup(s => s.CurrentPowerToGridXmlAttributeHeaderName()).Returns("Type");
        Mock.Mock<IConfigurationWrapper>().Setup(s => s.CurrentPowerToGridXmlAttributeHeaderValue()).Returns("GridPower");
        Mock.Mock<IConfigurationWrapper>().Setup(s => s.CurrentPowerToGridXmlAttributeValueName()).Returns("Value");

        var intValue = gridService.GetValueFromResult("Device/Measurements/Measurement", xml, NodePatternType.Xml, true);

        Assert.Equal(384, intValue);
    }

    [Theory]
    [InlineData("384.8746")]
    [InlineData("384")]
    [InlineData("384.0")]
    [InlineData("384.147")]
    public void Can_Get_Integer_From_Inverter_Xml_Attribute_Result(string text)
    {
        var xml = string.Format(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<Device Name=\"PIKO 4.6-2 MP plus\" Type=\"Inverter\" Platform=\"Net16\" HmiPlatform=\"HMI17\" NominalPower=\"4600\" UserPowerLimit=\"nan\" CountryPowerLimit=\"nan\" Serial=\"XXXXXXXXXXXXXXXXXXXX\" OEMSerial=\"XXXXXXXX\" BusAddress=\"1\" NetBiosName=\"XXXXXXXXXXXXXXX\" WebPortal=\"PIKO Solar Portal\" ManufacturerURL=\"kostal-solar-electric.com\" IpAddress=\"192.168.XXX.XXX\" DateTime=\"2022-06-08T19:33:25\" MilliSeconds=\"806\">\r\n  <Measurements>\r\n    <Measurement Value=\"231.3\" Unit=\"V\" Type=\"AC_Voltage\"/>\r\n    <Measurement Value=\"1.132\" Unit=\"A\" Type=\"AC_Current\"/>\r\n    <Measurement Value=\"{0}\" Unit=\"W\" Type=\"AC_Power\"/>\r\n    <Measurement Value=\"264.3\" Unit=\"W\" Type=\"AC_Power_fast\"/>\r\n    <Measurement Value=\"49.992\" Unit=\"Hz\" Type=\"AC_Frequency\"/>\r\n    <Measurement Value=\"474.2\" Unit=\"V\" Type=\"DC_Voltage\"/>\r\n    <Measurement Value=\"0.594\" Unit=\"A\" Type=\"DC_Current\"/>\r\n    <Measurement Value=\"473.5\" Unit=\"V\" Type=\"LINK_Voltage\"/>\r\n    <Measurement Value=\"18.7\" Unit=\"W\" Type=\"GridPower\"/>\r\n    <Measurement Value=\"0.0\" Unit=\"W\" Type=\"GridConsumedPower\"/>\r\n    <Measurement Value=\"18.7\" Unit=\"W\" Type=\"GridInjectedPower\"/>\r\n    <Measurement Value=\"237.3\" Unit=\"W\" Type=\"OwnConsumedPower\"/>\r\n    <Measurement Value=\"100.0\" Unit=\"%\" Type=\"Derating\"/>\r\n  </Measurements>\r\n</Device>", text);
        var gridService = Mock.Create<TeslaSolarCharger.Server.Services.GridService>();
        Mock.Mock<IConfigurationWrapper>().Setup(s => s.CurrentInverterPowerXmlAttributeHeaderName()).Returns("Type");
        Mock.Mock<IConfigurationWrapper>().Setup(s => s.CurrentInverterPowerXmlAttributeHeaderValue()).Returns("AC_Power");
        Mock.Mock<IConfigurationWrapper>().Setup(s => s.CurrentInverterPowerXmlAttributeValueName()).Returns("Value");

        var intValue = gridService.GetValueFromResult("Device/Measurements/Measurement", xml, NodePatternType.Xml, false);

        Assert.Equal(384, intValue);
    }

    [Theory]
    [InlineData("384.8746")]
    [InlineData("384")]
    [InlineData("384.0")]
    [InlineData("384.147")]
    public void Can_Get_Integer_From_Xml_Node_Result(string text)
    {
        var xml = string.Format(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<Device Name=\"PIKO 4.6-2 MP plus\" Type=\"Inverter\" Platform=\"Net16\" HmiPlatform=\"HMI17\" NominalPower=\"4600\" UserPowerLimit=\"nan\" CountryPowerLimit=\"nan\" Serial=\"XXXXXXXXXXXXXXXXXXXX\" OEMSerial=\"XXXXXXXX\" BusAddress=\"1\" NetBiosName=\"XXXXXXXXXXXXXXX\" WebPortal=\"PIKO Solar Portal\" ManufacturerURL=\"kostal-solar-electric.com\" IpAddress=\"192.168.XXX.XXX\" DateTime=\"2022-06-08T19:33:25\" MilliSeconds=\"806\">\r\n  <Measurements>\r\n    <InverterPower>1000</InverterPower>\r\n    <GridPower>{0}</GridPower>\r\n  </Measurements>\r\n</Device>", text);
        var gridService = Mock.Create<TeslaSolarCharger.Server.Services.GridService>();

        var intValue = gridService.GetValueFromResult("Device/Measurements/GridPower", xml, NodePatternType.Xml, false);

        Assert.Equal(384, intValue);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData(" ", null)]
    [InlineData(null, "")]
    [InlineData(null, " ")]
    public void Decides_Correct_Note_Pattern_Type_None(string jsonPattern, string xmlPattern)
    {
        var gridService = Mock.Create<TeslaSolarCharger.Server.Services.GridService>();
        var nodePatternType = gridService.DecideNotePatternType(jsonPattern, xmlPattern);

        Assert.Equal(NodePatternType.None, nodePatternType);
    }

    [Theory]
    [InlineData("$.data.overage", null)]
    [InlineData("$.data.overage", "")]
    [InlineData("$.data.overage", " ")]
    public void Decides_Correct_Note_Pattern_Type_Json(string jsonPattern, string xmlPattern)
    {
        var gridService = Mock.Create<TeslaSolarCharger.Server.Services.GridService>();
        var nodePatternType = gridService.DecideNotePatternType(jsonPattern, xmlPattern);

        Assert.Equal(NodePatternType.Json, nodePatternType);
    }

    [Theory]
    [InlineData(null, "Device/Measurements/Measurement")]
    [InlineData("", "Device/Measurements/Measurement")]
    [InlineData(" ", "Device/Measurements/Measurement")]
    public void Decides_Correct_Note_Pattern_Type_Xml(string jsonPattern, string xmlPattern)
    {
        var gridService = Mock.Create<TeslaSolarCharger.Server.Services.GridService>();
        var nodePatternType = gridService.DecideNotePatternType(jsonPattern, xmlPattern);

        Assert.Equal(NodePatternType.Xml, nodePatternType);
    }
}