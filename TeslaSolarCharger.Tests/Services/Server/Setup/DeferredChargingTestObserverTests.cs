using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Dtos.Setup;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.TimeProviding;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server.Setup;

/// <summary>
/// A postponed charging test settles itself by the equipment actually charging. Nothing here sends a command: the
/// proof that setup worked is the car charging, not a command that was accepted.
/// </summary>
public class DeferredChargingTestObserverTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 8, 0, 0, TimeSpan.Zero);

    private readonly Mock<ISettings> _settings = new();
    private readonly Mock<IDeferredSetupCheckService> _deferredSetupCheckService = new();

    private List<DtoDeferredSetupCheck> _checks = new();

    public DeferredChargingTestObserverTests()
    {
        _settings.Setup(s => s.Cars).Returns(new List<DtoCar>());
        _settings.Setup(s => s.OcppConnectorStates).Returns(new ConcurrentDictionary<int, DtoOcppConnectorState>());
        _deferredSetupCheckService.Setup(s => s.GetDeferredChecks()).ReturnsAsync(() => _checks);
    }

    private DeferredChargingTestObserver NewObserver() => new(
        Mock.Of<ILogger<DeferredChargingTestObserver>>(),
        _settings.Object,
        _deferredSetupCheckService.Object,
        new FakeDateTimeProvider(Now.UtcDateTime));

    private static DtoCar Car(int id, bool isCharging, bool isHome, TimeSpan? age = null)
    {
        var timestamp = Now - (age ?? TimeSpan.Zero);
        return new DtoCar
        {
            Id = id,
            IsCharging = new DtoTimeStampedValue<bool?>(timestamp, isCharging),
            IsHomeGeofence = new DtoTimeStampedValue<bool?>(timestamp, isHome),
        };
    }

    private static DtoDeferredSetupCheck CarCheck(int carId, SetupCheckResultState state = SetupCheckResultState.NotRun) => new()
    {
        Kind = DeferredSetupCheckKind.RealChargingTest,
        DeviceKind = SetupDeviceKind.Car,
        DeviceId = carId,
        State = state,
    };

    private void ExpectSettled(DtoDeferredSetupCheck check) =>
        _deferredSetupCheckService.Verify(
            s => s.RecordCheckResult(check.Id, SetupCheckResultState.Succeeded, null), Times.Once);

    private void ExpectNotSettled() =>
        _deferredSetupCheckService.Verify(
            s => s.RecordCheckResult(It.IsAny<Guid>(), It.IsAny<SetupCheckResultState>(), It.IsAny<string?>()), Times.Never);

    [Fact]
    public async Task ACarChargingAtHomeSettlesItsTest()
    {
        var check = CarCheck(5);
        _checks = new List<DtoDeferredSetupCheck> { check, };
        _settings.Setup(s => s.Cars).Returns(new List<DtoCar> { Car(5, isCharging: true, isHome: true), });

        await NewObserver().ObserveRunningCharges(CancellationToken.None);

        ExpectSettled(check);
    }

    [Fact]
    public async Task ACarChargingSomewhereElseProvesNothing()
    {
        _checks = new List<DtoDeferredSetupCheck> { CarCheck(5), };
        //Charging at a public charger says nothing about whether this installation can control it.
        _settings.Setup(s => s.Cars).Returns(new List<DtoCar> { Car(5, isCharging: true, isHome: false), });

        await NewObserver().ObserveRunningCharges(CancellationToken.None);

        ExpectNotSettled();
    }

    [Fact]
    public async Task ACarThatIsHomeButNotChargingSettlesNothing()
    {
        _checks = new List<DtoDeferredSetupCheck> { CarCheck(5), };
        _settings.Setup(s => s.Cars).Returns(new List<DtoCar> { Car(5, isCharging: false, isHome: true), });

        await NewObserver().ObserveRunningCharges(CancellationToken.None);

        ExpectNotSettled();
    }

    [Fact]
    public async Task AStaleReadingIsNotTakenAsProof()
    {
        _checks = new List<DtoDeferredSetupCheck> { CarCheck(5), };
        //It said it was charging hours ago. That is not evidence about now.
        _settings.Setup(s => s.Cars).Returns(new List<DtoCar>
        {
            Car(5, isCharging: true, isHome: true, age: TimeSpan.FromHours(3)),
        });

        await NewObserver().ObserveRunningCharges(CancellationToken.None);

        ExpectNotSettled();
    }

    [Fact]
    public async Task ACheckForAnotherCarIsLeftAlone()
    {
        _checks = new List<DtoDeferredSetupCheck> { CarCheck(9), };
        _settings.Setup(s => s.Cars).Returns(new List<DtoCar> { Car(5, isCharging: true, isHome: true), });

        await NewObserver().ObserveRunningCharges(CancellationToken.None);

        ExpectNotSettled();
    }

    [Fact]
    public async Task AChargingConnectorSettlesItsOwnTest()
    {
        var check = new DtoDeferredSetupCheck
        {
            Kind = DeferredSetupCheckKind.RealChargingTest,
            DeviceKind = SetupDeviceKind.ChargingStationConnector,
            DeviceId = 3,
        };
        _checks = new List<DtoDeferredSetupCheck> { check, };
        var states = new ConcurrentDictionary<int, DtoOcppConnectorState>();
        states[3] = new DtoOcppConnectorState { IsCharging = new DtoTimeStampedValue<bool>(Now, true), };
        _settings.Setup(s => s.OcppConnectorStates).Returns(states);

        await NewObserver().ObserveRunningCharges(CancellationToken.None);

        ExpectSettled(check);
    }

    [Fact]
    public async Task NothingChargingMeansStorageIsNotEvenRead()
    {
        _checks = new List<DtoDeferredSetupCheck> { CarCheck(5), };
        _settings.Setup(s => s.Cars).Returns(new List<DtoCar> { Car(5, isCharging: false, isHome: true), });

        await NewObserver().ObserveRunningCharges(CancellationToken.None);

        //This runs inside the charging loop, so the common case must not cost a read at all.
        _deferredSetupCheckService.Verify(s => s.GetDeferredChecks(), Times.Never);
    }

    [Fact]
    public async Task ACheckThatAlreadySucceededIsNotRecordedAgain()
    {
        _checks = new List<DtoDeferredSetupCheck> { CarCheck(5, SetupCheckResultState.Succeeded), };
        _settings.Setup(s => s.Cars).Returns(new List<DtoCar> { Car(5, isCharging: true, isHome: true), });

        await NewObserver().ObserveRunningCharges(CancellationToken.None);

        ExpectNotSettled();
    }

    [Fact]
    public async Task AFailedCheckGetsAnotherChance()
    {
        var check = CarCheck(5, SetupCheckResultState.Failed);
        _checks = new List<DtoDeferredSetupCheck> { check, };
        _settings.Setup(s => s.Cars).Returns(new List<DtoCar> { Car(5, isCharging: true, isHome: true), });

        await NewObserver().ObserveRunningCharges(CancellationToken.None);

        ExpectSettled(check);
    }

    [Fact]
    public async Task AProblemReadingTheChecksNeverDisturbsCharging()
    {
        _deferredSetupCheckService.Setup(s => s.GetDeferredChecks()).ThrowsAsync(new InvalidOperationException("db down"));
        _settings.Setup(s => s.Cars).Returns(new List<DtoCar> { Car(5, isCharging: true, isHome: true), });

        //This runs inside the charging loop, so it has to swallow its own trouble.
        await NewObserver().ObserveRunningCharges(CancellationToken.None);

        ExpectNotSettled();
    }
}
