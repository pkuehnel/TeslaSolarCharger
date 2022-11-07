using System;
using System.Diagnostics;
using System.IO;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Enums;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Wrappers;

public class ConfigurationWrapper : TestBase
{
    public ConfigurationWrapper(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public void Get_Not_Nullable_String()
    {
        var configurationWrapper = Mock.Create<Shared.Wrappers.ConfigurationWrapper>();

        var existingConfigValue = "TeslaMateApiBaseUrl";
        var teslaMateApiBaseUrl = 
            configurationWrapper.GetNotNullableConfigurationValue<string>(existingConfigValue);
        
        Assert.Equal("http://192.168.1.50:8097", teslaMateApiBaseUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("notExisiting")]
    public void Throw_Exception_On_Null_String(string notExisitingConfigValue)
    {
        var configurationWrapper = Mock.Create<Shared.Wrappers.ConfigurationWrapper>();
        Assert.Throws<NullReferenceException>(
            () => configurationWrapper.GetNotNullableConfigurationValue<string>(notExisitingConfigValue));
    }

    [Theory]
    [InlineData("")]
    [InlineData("notExisiting")]
    public void Returns_Null_On_Non_Exisiting_Values(string notExisitingConfigValue)
    {
        var configurationWrapper = Mock.Create<Shared.Wrappers.ConfigurationWrapper>();
        var value = configurationWrapper.GetNullableConfigurationValue<string>(notExisitingConfigValue);

        Assert.Null(value);
    }

    [Theory]
    [InlineData("ten")]
    [InlineData("one")]
    [InlineData("zero")]
    [InlineData("notExisiting")]
    public void Get_TimeSpan_From_Minutes(string configName)
    {
        var configurationWrapper = Mock.Create<Shared.Wrappers.ConfigurationWrapper>();
        var timespan =
            configurationWrapper.GetMinutesConfigurationValueIfGreaterThanMinumum(configName, TimeSpan.FromMinutes(1));

        switch (configName)
        {
            case "ten":
                Assert.Equal(TimeSpan.FromMinutes(10), timespan);
                break;
            case "one":
                Assert.Equal(TimeSpan.FromMinutes(1), timespan);
                break;
            case "zero":
                Assert.Equal(TimeSpan.FromMinutes(1), timespan);
                break;
            case "notExisiting":
                Assert.Equal(TimeSpan.FromMinutes(1), timespan);
                break;
            default:
                throw new NotImplementedException("Config name not converd in this test");
        }
    }

    [Theory]
    [InlineData("ten")]
    [InlineData("one")]
    [InlineData("zero")]
    [InlineData("notExisiting")]
    public void Get_TimeSpan_From_Seconds(string configName)
    {
        var configurationWrapper = Mock.Create<Shared.Wrappers.ConfigurationWrapper>();
        var minimum = TimeSpan.FromSeconds(1);
        var timespan =
            configurationWrapper.GetSecondsConfigurationValueIfGreaterThanMinumum(configName, minimum);

        switch (configName)
        {
            case "ten":
                Assert.Equal(TimeSpan.FromSeconds(10), timespan);
                break;
            case "one":
                Assert.Equal(TimeSpan.FromSeconds(1), timespan);
                break;
            case "zero":
                Assert.Equal(TimeSpan.FromSeconds(1), timespan);
                break;
            case "notExisiting":
                Assert.Equal(TimeSpan.FromSeconds(1), timespan);
                break;
            default:
                throw new NotImplementedException("Config name not converd in this test");
        }
    }

    [Fact]
    public void GetConfigurationFileDirectory()
    {
        var configurationWrapper = Mock.Create<Shared.Wrappers.ConfigurationWrapper>();
        var value = configurationWrapper.ConfigFileDirectory();
        var dirInfo = new DirectoryInfo(value);
        Assert.Equal("configs", dirInfo.Name);
    }

    [Fact]
    public void GetCarConfigurationFileFullName()
    {
        var configurationWrapper = Mock.Create<Shared.Wrappers.ConfigurationWrapper>();
        var value = configurationWrapper.CarConfigFileFullName();
        var fileInfo = new FileInfo(value);
        Assert.Equal("carConfig.json", fileInfo.Name);
    }

    [Fact]
    public void SetsCorrectNoValueSources()
    {
        var configurationWrapper = Mock.Create<Shared.Wrappers.ConfigurationWrapper>();
        var dtoBaseConfiguration = new DtoBaseConfiguration();
        configurationWrapper.CreateDefaultFrontendConfiguration(dtoBaseConfiguration);

        Assert.NotNull(dtoBaseConfiguration.FrontendConfiguration);
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.GridValueSource);
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.InverterValueSource);
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.HomeBatteryValueSource);
    }

    [Fact]
    public void SetsCorrectGridOnlyRestValueSources()
    {
        var configurationWrapper = Mock.Create<Shared.Wrappers.ConfigurationWrapper>();
        var dtoBaseConfiguration = new DtoBaseConfiguration();
        dtoBaseConfiguration.CurrentPowerToGridUrl = "http://192.168.1.50:5007/api/ChargingLog/GetCurrentGridPower";

        configurationWrapper.CreateDefaultFrontendConfiguration(dtoBaseConfiguration);

        Assert.NotNull(dtoBaseConfiguration.FrontendConfiguration);
        Assert.Equal(SolarValueSource.Rest, dtoBaseConfiguration.FrontendConfiguration.GridValueSource);
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.InverterValueSource);
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.HomeBatteryValueSource);
    }

    [Fact]
    public void SetsCorrectHomeBatteryOnlyRestValueSources()
    {
        var configurationWrapper = Mock.Create<Shared.Wrappers.ConfigurationWrapper>();
        var dtoBaseConfiguration = new DtoBaseConfiguration();
        dtoBaseConfiguration.HomeBatteryPowerUrl = "http://192.168.1.50:5007/api/ChargingLog/GetCurrentGridPower";

        configurationWrapper.CreateDefaultFrontendConfiguration(dtoBaseConfiguration);

        Assert.NotNull(dtoBaseConfiguration.FrontendConfiguration);
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.GridValueSource);
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.InverterValueSource);
        Assert.Equal(SolarValueSource.Rest, dtoBaseConfiguration.FrontendConfiguration.HomeBatteryValueSource);
    }

    [Fact]
    public void SetsCorrectInverterOnlyRestValueSources()
    {
        var configurationWrapper = Mock.Create<Shared.Wrappers.ConfigurationWrapper>();
        var dtoBaseConfiguration = new DtoBaseConfiguration();
        dtoBaseConfiguration.CurrentInverterPowerUrl = "http://192.168.1.50:5007/api/ChargingLog/GetCurrentGridPower";

        configurationWrapper.CreateDefaultFrontendConfiguration(dtoBaseConfiguration);

        Assert.NotNull(dtoBaseConfiguration.FrontendConfiguration);
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.GridValueSource);
        Assert.Equal(SolarValueSource.Rest, dtoBaseConfiguration.FrontendConfiguration.InverterValueSource);
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.HomeBatteryValueSource);
    }

    [Fact]
    public void SetsCorrectGridOnlyModbusValueSources()
    {
        var configurationWrapper = Mock.Create<Shared.Wrappers.ConfigurationWrapper>();
        var dtoBaseConfiguration = new DtoBaseConfiguration();
        dtoBaseConfiguration.CurrentPowerToGridUrl = "http://192.168.1.50:5007/api/ChargingLog/GetCurrentGridPower";
        dtoBaseConfiguration.IsModbusGridUrl = true;

        configurationWrapper.CreateDefaultFrontendConfiguration(dtoBaseConfiguration);

        Assert.NotNull(dtoBaseConfiguration.FrontendConfiguration);
        Assert.Equal(SolarValueSource.Modbus, dtoBaseConfiguration.FrontendConfiguration.GridValueSource);
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.InverterValueSource);
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.HomeBatteryValueSource);
    }

    [Fact]
    public void SetsCorrectHomeBatteryOnlyModbusValueSources()
    {
        var configurationWrapper = Mock.Create<Shared.Wrappers.ConfigurationWrapper>();
        var dtoBaseConfiguration = new DtoBaseConfiguration();
        dtoBaseConfiguration.HomeBatteryPowerUrl = "http://192.168.1.50:5007/api/ChargingLog/GetCurrentGridPower";
        dtoBaseConfiguration.IsModbusHomeBatteryPowerUrl = true;

        configurationWrapper.CreateDefaultFrontendConfiguration(dtoBaseConfiguration);

        Assert.NotNull(dtoBaseConfiguration.FrontendConfiguration);
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.GridValueSource);
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.InverterValueSource);
        Assert.Equal(SolarValueSource.Modbus, dtoBaseConfiguration.FrontendConfiguration.HomeBatteryValueSource);
    }

    [Fact]
    public void SetsCorrectInverterOnlyModbusValueSources()
    {
        var configurationWrapper = Mock.Create<Shared.Wrappers.ConfigurationWrapper>();
        var dtoBaseConfiguration = new DtoBaseConfiguration();
        dtoBaseConfiguration.CurrentInverterPowerUrl = "http://192.168.1.50:5007/api/ChargingLog/GetCurrentGridPower";
        dtoBaseConfiguration.IsModbusCurrentInverterPowerUrl = true;

        configurationWrapper.CreateDefaultFrontendConfiguration(dtoBaseConfiguration);

        Assert.NotNull(dtoBaseConfiguration.FrontendConfiguration);
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.GridValueSource);
        Assert.Equal(SolarValueSource.Modbus, dtoBaseConfiguration.FrontendConfiguration.InverterValueSource);
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.HomeBatteryValueSource);
    }

    [Fact]
    public void SetsCorrectGridOnlyMqttValueSources()
    {
        var configurationWrapper = Mock.Create<Shared.Wrappers.ConfigurationWrapper>();
        var dtoBaseConfiguration = new DtoBaseConfiguration();
        dtoBaseConfiguration.CurrentPowerToGridMqttTopic = "power";

        configurationWrapper.CreateDefaultFrontendConfiguration(dtoBaseConfiguration);

        Assert.NotNull(dtoBaseConfiguration.FrontendConfiguration);
        Assert.Equal(SolarValueSource.Mqtt, dtoBaseConfiguration.FrontendConfiguration.GridValueSource);
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.InverterValueSource);
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.HomeBatteryValueSource);
    }

    [Fact]
    public void SetsCorrectHomeBatteryOnlyMqttValueSources()
    {
        var configurationWrapper = Mock.Create<Shared.Wrappers.ConfigurationWrapper>();
        var dtoBaseConfiguration = new DtoBaseConfiguration();
        dtoBaseConfiguration.HomeBatterySocMqttTopic = "power";

        configurationWrapper.CreateDefaultFrontendConfiguration(dtoBaseConfiguration);

        Assert.NotNull(dtoBaseConfiguration.FrontendConfiguration);
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.GridValueSource);
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.InverterValueSource);
        Assert.Equal(SolarValueSource.Mqtt, dtoBaseConfiguration.FrontendConfiguration.HomeBatteryValueSource);
    }

    [Fact]
    public void SetsCorrectInverterOnlyMqttValueSources()
    {
        var configurationWrapper = Mock.Create<Shared.Wrappers.ConfigurationWrapper>();
        var dtoBaseConfiguration = new DtoBaseConfiguration();
        dtoBaseConfiguration.CurrentInverterPowerMqttTopic = "power";

        configurationWrapper.CreateDefaultFrontendConfiguration(dtoBaseConfiguration);

        Assert.NotNull(dtoBaseConfiguration.FrontendConfiguration);
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.GridValueSource);
        Assert.Equal(SolarValueSource.Mqtt, dtoBaseConfiguration.FrontendConfiguration.InverterValueSource);
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.HomeBatteryValueSource);
    }
}
