using System;
using Xunit;
using Xunit.Abstractions;

namespace SmartTeslaAmpSetter.Tests.Services;

public class ConfigurationService : TestBase
{
    public ConfigurationService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public void Get_Not_Nullable_String()
    {
        var configurationService = Mock.Create<Server.Services.ConfigurationService>();

        var existingConfigValue = "TeslaMateApiBaseUrl";
        var teslaMateApiBaseUrl = 
            configurationService.GetNotNullableConfigurationValue(existingConfigValue);
        
        Assert.Equal("http://192.168.1.50:8097", teslaMateApiBaseUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("notExisiting")]
    public void Throw_Exception_On_Null_String(string notExisitingConfigValue)
    {
        var configurationService = Mock.Create<Server.Services.ConfigurationService>();
        Assert.Throws<NullReferenceException>(
            () => configurationService.GetNotNullableConfigurationValue(notExisitingConfigValue));
    }

    [Theory]
    [InlineData("")]
    [InlineData("notExisiting")]
    public void Returns_Null_On_Non_Exisiting_Values(string notExisitingConfigValue)
    {
        var configurationService = Mock.Create<Server.Services.ConfigurationService>();
        var value = configurationService.GetNullableConfigurationValue(notExisitingConfigValue);

        Assert.Null(value);
    }

    [Theory]
    [InlineData("ten")]
    [InlineData("one")]
    [InlineData("zero")]
    [InlineData("notExisiting")]
    public void Get_TimeSpan_From_Minutes(string configName)
    {
        var configurationService = Mock.Create<Server.Services.ConfigurationService>();
        var timespan =
            configurationService.GetMinutesConfigurationValueIfGreaterThanMinumum(configName, TimeSpan.FromMinutes(1));

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
        var configurationService = Mock.Create<Server.Services.ConfigurationService>();
        var minimum = TimeSpan.FromSeconds(1);
        var timespan =
            configurationService.GetSecondsConfigurationValueIfGreaterThanMinumum(configName, minimum);

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
}