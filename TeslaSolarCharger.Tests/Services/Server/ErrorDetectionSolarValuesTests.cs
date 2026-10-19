using Autofac;
using System;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Tests.Helper;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

/// <summary>
/// When "Solar values are not available" is shown: the solar values were not updated for three runs while at least
/// one device is configured.
/// </summary>
public class ErrorDetectionSolarValuesTests : TestBase
{
    private static readonly TimeSpan PvValueJobInterval = TimeSpan.FromSeconds(10);

    public ErrorDetectionSolarValuesTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.PvValueJobUpdateIntervall()).Returns(PvValueJobInterval);
    }

    private ErrorDetectionService CreateService(params IGenericValue<decimal>[] devices) => Mock.Create<ErrorDetectionService>(
        new TypedParameter(typeof(IGenericValueService),
            GenericValueTestDevices.Registry(GenericValueTestDevices.HandlingService(devices))));

    private IGenericValue<decimal> Device(ConfigurationType configurationType) =>
        GenericValueTestDevices.Device(configurationType, CurrentFakeDate);

    [Theory]
    [InlineData(ConfigurationType.TemplateValue)]
    [InlineData(ConfigurationType.RestSolarValue)]
    [InlineData(ConfigurationType.ModbusSolarValue)]
    [InlineData(ConfigurationType.MqttSolarValue)]
    public void OldValuesOfAnyConfiguredDeviceAreReported(ConfigurationType configurationType)
    {
        var service = CreateService(Device(configurationType));

        Assert.True(service.AreSolarValuesTooOld(TimeSpan.FromSeconds(31)));
    }

    [Theory]
    [InlineData(29, false)]
    //Exactly three runs is not too old yet.
    [InlineData(30, false)]
    [InlineData(31, true)]
    public void ValuesAreTooOldAfterThreeRuns(int ageSeconds, bool expectedTooOld)
    {
        var service = CreateService(Device(ConfigurationType.TemplateValue));

        Assert.Equal(expectedTooOld, service.AreSolarValuesTooOld(TimeSpan.FromSeconds(ageSeconds)));
    }

    [Fact]
    public void WithoutAnyDeviceNothingIsReported()
    {
        var service = CreateService();

        Assert.False(service.AreSolarValuesTooOld(TimeSpan.FromDays(1)));
    }

    [Fact]
    public void TheFakeDeviceAloneIsNoConfiguredDevice()
    {
        var service = CreateService(Device(ConfigurationType.FakeSolarValue));

        Assert.False(service.AreSolarValuesTooOld(TimeSpan.FromDays(1)));
    }
}
