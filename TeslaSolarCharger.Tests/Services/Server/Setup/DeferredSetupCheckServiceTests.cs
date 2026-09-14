using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Setup;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources;
using TeslaSolarCharger.Shared.TimeProviding;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server.Setup;

public class DeferredSetupCheckServiceTests
{
    private readonly Mock<ITscConfigurationService> _tscConfigurationService = new();
    private readonly Constants _constants = new();
    private string? _storedJson;

    public DeferredSetupCheckServiceTests()
    {
        _tscConfigurationService
            .Setup(s => s.GetConfigurationValueByKey(_constants.DeferredSetupChecksKey))
            .ReturnsAsync(() => _storedJson);
        _tscConfigurationService
            .Setup(s => s.SetConfigurationValueByKey(_constants.DeferredSetupChecksKey, It.IsAny<string>()))
            .Callback<string, string>((_, value) => _storedJson = value)
            .Returns(Task.CompletedTask);
    }

    private DeferredSetupCheckService NewService() => new(
        Mock.Of<ILogger<DeferredSetupCheckService>>(),
        _tscConfigurationService.Object,
        new FakeDateTimeProvider(new DateTime(2026, 9, 15, 8, 0, 0, DateTimeKind.Utc)),
        _constants);

    private static DtoDeferredSetupCheck ChargingTestFor(int carId) => new()
    {
        Kind = DeferredSetupCheckKind.RealChargingTest,
        DeviceKind = SetupDeviceKind.Car,
        DeviceId = carId,
        DisplayName = "Car",
    };

    [Fact]
    public async Task NothingStored_MeansNothingIsOutstanding()
    {
        Assert.Empty(await NewService().GetDeferredChecks());
    }

    [Fact]
    public async Task UnreadableStorage_IsIgnoredInsteadOfThrowing()
    {
        _storedJson = "{broken";

        Assert.Empty(await NewService().GetDeferredChecks());
    }

    [Fact]
    public async Task APostponedCheckIsRemembered()
    {
        var service = NewService();

        await service.AddOrUpdateDeferredCheck(ChargingTestFor(4));

        var check = Assert.Single(await service.GetDeferredChecks());
        Assert.Equal(DeferredSetupCheckKind.RealChargingTest, check.Kind);
        Assert.Equal(4, check.DeviceId);
        Assert.Equal(SetupCheckResultState.NotRun, check.State);
    }

    [Fact]
    public async Task PostponingTheSameCheckTwiceDoesNotCollectDuplicateReminders()
    {
        var service = NewService();

        await service.AddOrUpdateDeferredCheck(ChargingTestFor(4));
        await service.AddOrUpdateDeferredCheck(ChargingTestFor(4));

        Assert.Single(await service.GetDeferredChecks());
    }

    [Fact]
    public async Task ChecksForDifferentDevicesAreKeptApart()
    {
        var service = NewService();

        await service.AddOrUpdateDeferredCheck(ChargingTestFor(4));
        await service.AddOrUpdateDeferredCheck(ChargingTestFor(5));

        Assert.Equal(2, (await service.GetDeferredChecks()).Count);
    }

    [Fact]
    public async Task ARecordedResultIsStoredWithTheTimeItWasTried()
    {
        var service = NewService();
        var check = await service.AddOrUpdateDeferredCheck(ChargingTestFor(4));

        await service.RecordCheckResult(check.Id, SetupCheckResultState.Succeeded, "Charging started");

        var stored = Assert.Single(await service.GetDeferredChecks());
        Assert.Equal(SetupCheckResultState.Succeeded, stored.State);
        Assert.Equal("Charging started", stored.LastResultMessage);
        Assert.NotNull(stored.LastAttemptedAt);
    }

    [Fact]
    public async Task RecordingAResultForSomethingThatIsNotOutstandingChangesNothing()
    {
        var service = NewService();
        await service.AddOrUpdateDeferredCheck(ChargingTestFor(4));

        await service.RecordCheckResult(Guid.NewGuid(), SetupCheckResultState.Failed, "nope");

        Assert.Equal(SetupCheckResultState.NotRun, Assert.Single(await service.GetDeferredChecks()).State);
    }

    [Fact]
    public async Task ACheckCanBeRemoved()
    {
        var service = NewService();
        var check = await service.AddOrUpdateDeferredCheck(ChargingTestFor(4));

        await service.RemoveDeferredCheck(check.Id);

        Assert.Empty(await service.GetDeferredChecks());
    }

    [Fact]
    public async Task AChangedConfigurationInvalidatesAnOlderResult()
    {
        var service = NewService();
        var check = ChargingTestFor(4);
        check.ConfigurationFingerprint = "bluetooth";
        await service.AddOrUpdateDeferredCheck(check);
        await service.RecordCheckResult(check.Id, SetupCheckResultState.Succeeded, "worked");

        await service.ReevaluateChecksForDevice(SetupDeviceKind.Car, 4, "cloud");

        var stored = Assert.Single(await service.GetDeferredChecks());
        //A result recorded against the old configuration says nothing about the new one.
        Assert.Equal(SetupCheckResultState.NotRun, stored.State);
        Assert.Null(stored.LastResultMessage);
        Assert.Equal("cloud", stored.ConfigurationFingerprint);
    }

    [Fact]
    public async Task AnUnchangedConfigurationKeepsItsResult()
    {
        var service = NewService();
        var check = ChargingTestFor(4);
        check.ConfigurationFingerprint = "bluetooth";
        await service.AddOrUpdateDeferredCheck(check);
        await service.RecordCheckResult(check.Id, SetupCheckResultState.Succeeded, "worked");

        await service.ReevaluateChecksForDevice(SetupDeviceKind.Car, 4, "bluetooth");

        Assert.Equal(SetupCheckResultState.Succeeded, Assert.Single(await service.GetDeferredChecks()).State);
    }

    [Fact]
    public async Task AnotherDevicesChangeLeavesThisCheckAlone()
    {
        var service = NewService();
        var check = ChargingTestFor(4);
        check.ConfigurationFingerprint = "bluetooth";
        await service.AddOrUpdateDeferredCheck(check);
        await service.RecordCheckResult(check.Id, SetupCheckResultState.Succeeded, "worked");

        await service.ReevaluateChecksForDevice(SetupDeviceKind.Car, 99, "cloud");

        Assert.Equal(SetupCheckResultState.Succeeded, Assert.Single(await service.GetDeferredChecks()).State);
    }

    [Fact]
    public async Task DeferredChecksAreStoredApartFromTheSetupState()
    {
        var service = NewService();

        await service.AddOrUpdateDeferredCheck(ChargingTestFor(4));

        //Finishing setup clears the setup key; a postponed charging test must not go with it.
        _tscConfigurationService.Verify(
            s => s.SetConfigurationValueByKey(_constants.SetupCacheKey, It.IsAny<string>()), Times.Never);
        _tscConfigurationService.Verify(
            s => s.SetConfigurationValueByKey(_constants.DeferredSetupChecksKey, It.IsAny<string>()), Times.Once);
    }
}
