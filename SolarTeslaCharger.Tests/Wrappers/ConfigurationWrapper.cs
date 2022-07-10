using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace SolarTeslaCharger.Tests.Wrappers;

public class ConfigurationWrapper : TestBase
{
    public ConfigurationWrapper(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public void Get_Not_Nullable_String()
    {
        var configurationWrapper = Mock.Create<SolarTeslaCharger.Shared.Wrappers.ConfigurationWrapper>();

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
        var configurationWrapper = Mock.Create<SolarTeslaCharger.Shared.Wrappers.ConfigurationWrapper>();
        Assert.Throws<NullReferenceException>(
            () => configurationWrapper.GetNotNullableConfigurationValue<string>(notExisitingConfigValue));
    }

    [Theory]
    [InlineData("")]
    [InlineData("notExisiting")]
    public void Returns_Null_On_Non_Exisiting_Values(string notExisitingConfigValue)
    {
        var configurationWrapper = Mock.Create<SolarTeslaCharger.Shared.Wrappers.ConfigurationWrapper>();
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
        var configurationWrapper = Mock.Create<SolarTeslaCharger.Shared.Wrappers.ConfigurationWrapper>();
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
        var configurationWrapper = Mock.Create<SolarTeslaCharger.Shared.Wrappers.ConfigurationWrapper>();
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
        var configurationWrapper = Mock.Create<SolarTeslaCharger.Shared.Wrappers.ConfigurationWrapper>();
        var value = configurationWrapper.ConfigFileDirectory();

        Assert.Equal("configs", value);
    }

    [Fact]
    public void GetCarConfigurationFileFullName()
    {
        var configurationWrapper = Mock.Create<SolarTeslaCharger.Shared.Wrappers.ConfigurationWrapper>();
        var value = configurationWrapper.CarConfigFileFullName();
        var pathSeparator = Path.DirectorySeparatorChar;
        var linuxPathSeparator = '/';
        var windowsPathSeparator = '\\';
        Assert.True(pathSeparator.Equals(linuxPathSeparator) || pathSeparator.Equals(windowsPathSeparator));
        Assert.Equal($"configs{pathSeparator}carConfig.json", value);
    }
}