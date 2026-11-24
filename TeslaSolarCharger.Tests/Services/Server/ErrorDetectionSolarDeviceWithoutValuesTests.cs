using Autofac;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Enums;
using Moq;
using TeslaSolarCharger.SharedModel.Enums;
using TeslaSolarCharger.Tests.Helper;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

/// <summary>
/// The warning for a device that did not deliver values for a day, which is the only report for a device that only
/// delivers production.
/// </summary>
[SuppressMessage("ReSharper", "UseConfigureAwaitFalse")]
public class ErrorDetectionSolarDeviceWithoutValuesTests(ITestOutputHelper outputHelper) : TestBase(outputHelper)
{
    private IIssueKeys IssueKeys => Mock.Create<IIssueKeys>();

    private IGenericValue<decimal> FailingDevice(TimeSpan failingFor, int sourceId = 1,
        ConfigurationType configurationType = ConfigurationType.TemplateValue) =>
        GenericValueTestDevices.Device(configurationType, CurrentFakeDate, true, sourceId, CurrentFakeDate - failingFor,
            readings: (ValueUsage.InverterPower, 0));

    private IGenericValue<decimal> HealthyDevice(int sourceId) =>
        GenericValueTestDevices.Device(ConfigurationType.TemplateValue, CurrentFakeDate, sourceId: sourceId,
            readings: (ValueUsage.InverterPower, 0));

    private async Task<List<LoggedError>> ActiveWarnings() => await Context.LoggedErrors
        .Where(e => e.IssueKey.StartsWith(IssueKeys.SolarDeviceWithoutValues) && e.EndTimeStamp == null)
        .ToListAsync();

    /// <summary>The devices currently in the value registry.</summary>
    private List<IGenericValue<decimal>> _devices = new();

    private ErrorDetectionService? _service;

    /// <summary>One service as in the app, seeing the registry's devices of each run.</summary>
    private ErrorDetectionService Service
    {
        get
        {
            if (_service != default)
            {
                return _service;
            }
            var dateTimeProvider = new Mock<IDateTimeProvider>();
            dateTimeProvider.Setup(d => d.DateTimeOffSetUtcNow()).Returns(CurrentFakeDate);
            dateTimeProvider.Setup(d => d.UtcNow()).Returns(CurrentFakeDate.UtcDateTime);
            var handlingService = new Mock<IDecimalValueHandlingService>();
            handlingService.Setup(s => s.GetSnapshot()).Returns(() => _devices);
            _service = Mock.Create<ErrorDetectionService>(
                new TypedParameter(typeof(IDateTimeProvider), dateTimeProvider.Object),
                new TypedParameter(typeof(IGenericValueService), GenericValueTestDevices.Registry(handlingService.Object)));
            return _service;
        }
    }

    private async Task Detect(params IGenericValue<decimal>[] devices)
    {
        _devices = devices.ToList();
        var activeErrors = await Context.LoggedErrors.Where(e => e.EndTimeStamp == null).ToListAsync();
        await Service.DetectSolarDevicesWithoutValues(activeErrors);
    }

    [Fact]
    public async Task ADeviceFailingForMoreThanADayIsReported()
    {
        await Detect(FailingDevice(TimeSpan.FromHours(25), 3));

        var warning = Assert.Single(await ActiveWarnings());
        Assert.Equal(IssueKeys.SolarDeviceWithoutValues + ConfigurationType.TemplateValue + 3, warning.IssueKey);
        Assert.Contains("ID 3", warning.Message);
        Assert.Contains("Connection refused", warning.Message);
    }

    [Theory]
    [InlineData(1)]
    //A long winter night
    [InlineData(17)]
    //Exactly a day is not longer than a day.
    [InlineData(24)]
    public async Task ADeviceFailingForUpToADayIsNotReported(int failingHours)
    {
        await Detect(FailingDevice(TimeSpan.FromHours(failingHours)));

        Assert.Empty(await ActiveWarnings());
    }

    [Fact]
    public async Task AHealthyDeviceIsNotReported()
    {
        await Detect(HealthyDevice(1));

        Assert.Empty(await ActiveWarnings());
    }

    [Fact]
    public async Task TheFakeDeviceIsNotReported()
    {
        await Detect(FailingDevice(TimeSpan.FromDays(3), configurationType: ConfigurationType.FakeSolarValue));

        Assert.Empty(await ActiveWarnings());
    }

    [Fact]
    public async Task EveryFailingDeviceGetsItsOwnWarning()
    {
        await Detect(FailingDevice(TimeSpan.FromDays(2), 1), FailingDevice(TimeSpan.FromDays(2), 1, ConfigurationType.RestSolarValue),
            HealthyDevice(2));

        var warnings = await ActiveWarnings();
        Assert.Equal(2, warnings.Count);
        Assert.Equal(2, warnings.Select(w => w.IssueKey).Distinct().Count());
    }

    [Fact]
    public async Task TheWarningEndsWhenTheDeviceDeliversAgain()
    {
        await Detect(FailingDevice(TimeSpan.FromDays(2), 1));
        Assert.Single(await ActiveWarnings());

        await Detect(HealthyDevice(1));

        Assert.Empty(await ActiveWarnings());
    }

    [Fact]
    public async Task TheWarningEndsWhenTheDeviceIsRemoved()
    {
        await Detect(FailingDevice(TimeSpan.FromDays(2), 1));

        await Detect();

        Assert.Empty(await ActiveWarnings());
    }

    [Fact]
    public async Task AStillFailingDeviceKeepsOneWarning()
    {
        await Detect(FailingDevice(TimeSpan.FromDays(2), 1));
        await Detect(FailingDevice(TimeSpan.FromDays(2), 1));

        var warning = Assert.Single(await ActiveWarnings());
        Assert.Single(warning.FurtherOccurrences);
    }

    [Fact]
    public void TheWarningIsAWarningPerDevice()
    {
        var issue = Mock.Create<IPossibleIssues>().GetIssueByKey(IssueKeys.SolarDeviceWithoutValues + ConfigurationType.TemplateValue + 3);

        Assert.Equal(IssueSeverity.Warning, issue.IssueSeverity);
        Assert.True(issue.HasPlaceHolderIssueKey);
    }
}
