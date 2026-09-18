using Newtonsoft.Json;
using System;
using System.IO;
using System.Runtime.Caching;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Wrappers;

public class ConfigurationWrapper : TestBase
{
    public ConfigurationWrapper(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    /// <summary>
    /// The name the wrapper caches the base configuration JSON under. Seeding that cache is what lets a test decide
    /// what the wrapper reads without touching the file system: the file is only read on a cache miss.
    /// </summary>
    private const string BaseConfigurationCacheName = "baseConfiguration";

    /// <summary>
    /// Runs <paramref name="assertions"/> with <paramref name="configuration"/> as the stored base configuration.
    /// The cache is process wide, so it is cleared again afterwards and no other test sees this configuration.
    /// </summary>
    private static void WithStoredBaseConfiguration(DtoBaseConfiguration configuration, Action assertions)
    {
        MemoryCache.Default.Set(BaseConfigurationCacheName, JsonConvert.SerializeObject(configuration), new CacheItemPolicy());
        try
        {
            assertions();
        }
        finally
        {
            MemoryCache.Default.Remove(BaseConfigurationCacheName);
        }
    }

    [Theory]
    [InlineData(null, ConfigurationDefaults.HoldHomeBatteryChargeSocBuffer)]
    [InlineData(0, 0)]
    [InlineData(7, 7)]
    [InlineData(-10, -10)]
    public void HoldHomeBatteryChargeSocBuffer_UsesTheDefaultOnlyWhenNobodySetIt(int? stored, int expected)
    {
        var configurationWrapper = Mock.Create<Shared.Wrappers.ConfigurationWrapper>();
        var configuration = new DtoBaseConfiguration { HoldHomeBatteryChargeSocBuffer = stored, };

        WithStoredBaseConfiguration(configuration,
            () => Assert.Equal(expected, configurationWrapper.HoldHomeBatteryChargeSocBufferInPercent()));
    }

    [Theory]
    [InlineData(null, ConfigurationDefaults.ChargeHomeBatterySocBuffer)]
    [InlineData(0, 0)]
    [InlineData(7, 7)]
    [InlineData(-10, -10)]
    public void ChargeHomeBatterySocBuffer_UsesTheDefaultOnlyWhenNobodySetIt(int? stored, int expected)
    {
        var configurationWrapper = Mock.Create<Shared.Wrappers.ConfigurationWrapper>();
        var configuration = new DtoBaseConfiguration { ChargeHomeBatterySocBuffer = stored, };

        WithStoredBaseConfiguration(configuration,
            () => Assert.Equal(expected, configurationWrapper.ChargeHomeBatterySocBufferInPercent()));
    }

    /// <summary>
    /// A zero or negative interval would let every cycle readjust the charging power, so the wrapper replaces it.
    /// This is a guard against an unusable stored value, not the default a fresh configuration starts with.
    /// </summary>
    [Theory]
    [InlineData(25, 25)]
    [InlineData(1, 1)]
    [InlineData(0, 30)]
    [InlineData(-5, 30)]
    public void SkipPowerChangesInterval_ReplacesAnUnusableStoredValue(int stored, int expectedSeconds)
    {
        var configurationWrapper = Mock.Create<Shared.Wrappers.ConfigurationWrapper>();
        var configuration = new DtoBaseConfiguration { SkipPowerChangesOnLastAdjustmentNewerThanSeconds = stored, };

        WithStoredBaseConfiguration(configuration,
            () => Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), configurationWrapper.SkipPowerChangesOnLastAdjustmentNewerThan()));
    }

    /// <summary>
    /// Null means the user never decided, and since the setting became a default the app collects data over
    /// Bluetooth unless somebody switched it off. An explicit false has to keep switching it off.
    /// </summary>
    [Theory]
    [InlineData(null, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void GetVehicleDataViaBle_DefaultsToEnabledButKeepsAnExplicitChoice(bool? stored, bool expected)
    {
        var configurationWrapper = Mock.Create<Shared.Wrappers.ConfigurationWrapper>();
        var configuration = new DtoBaseConfiguration { GetVehicleDataViaBle = stored, };

        WithStoredBaseConfiguration(configuration,
            () => Assert.Equal(expected, configurationWrapper.GetVehicleDataViaBle()));
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
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.HomeBatteryValuesSource);
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
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.HomeBatteryValuesSource);
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
        Assert.Equal(SolarValueSource.Rest, dtoBaseConfiguration.FrontendConfiguration.HomeBatteryValuesSource);
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
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.HomeBatteryValuesSource);
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
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.HomeBatteryValuesSource);
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
        Assert.Equal(SolarValueSource.Modbus, dtoBaseConfiguration.FrontendConfiguration.HomeBatteryValuesSource);
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
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.HomeBatteryValuesSource);
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
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.HomeBatteryValuesSource);
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
        Assert.Equal(SolarValueSource.Mqtt, dtoBaseConfiguration.FrontendConfiguration.HomeBatteryValuesSource);
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
        Assert.Equal(SolarValueSource.None, dtoBaseConfiguration.FrontendConfiguration.HomeBatteryValuesSource);
    }
}
