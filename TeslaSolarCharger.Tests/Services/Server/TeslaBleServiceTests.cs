using PkSoftwareService.Custom.Backend.Ble;
using System.Collections.Generic;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Shared.Dtos.Ble;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;


/// <summary>
/// Covers how a failed BLE request is turned into something the user can act on. Getting this wrong sends people
/// looking for the wrong problem, e.g. re-pairing a key for a car that simply is not at home.
/// </summary>
public class TeslaBleServiceTests : TestBase
{
    private const string TestVin = "TESTVIN123456789A";

    public TeslaBleServiceTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public void ReadableChargeStateMeansEverythingWorks()
    {
        var result = TeslaBleService.ClassifyChargeState(new DtoBleCommandResult { Success = true, });

        Assert.Equal(BleConnectionTestResultType.Success, result);
    }

    [Theory]
    [InlineData(BleCommandOutcome.AdapterNotFound)]
    [InlineData(BleCommandOutcome.AdapterUnavailable)]
    [InlineData(BleCommandOutcome.WorkerError)]
    [InlineData(BleCommandOutcome.WorkerTimeout)]
    [InlineData(BleCommandOutcome.InvalidRequest)]
    public void LocalProblemsAreReportedAsContainerProblem(BleCommandOutcome outcome)
    {
        var result = TeslaBleService.ClassifyChargeState(new DtoBleCommandResult { Success = false, Outcome = outcome, });

        Assert.Equal(BleConnectionTestResultType.ContainerProblem, result);
    }

    [Fact]
    public void SleepingCarNeedsNoFurtherChecks()
    {
        var result = TeslaBleService.ClassifyChargeState(new DtoBleCommandResult
        {
            Success = false,
            Outcome = BleCommandOutcome.CarAsleep,
        });

        Assert.Equal(BleConnectionTestResultType.CarAsleep, result);
    }

    [Theory]
    [InlineData(BleCommandOutcome.CarAbsent)]
    [InlineData(BleCommandOutcome.LinkFailed)]
    [InlineData(BleCommandOutcome.CarRefused)]
    [InlineData(null)]
    public void UnclearOutcomesAreNarrowedDownFurther(BleCommandOutcome? outcome)
    {
        var result = TeslaBleService.ClassifyChargeState(new DtoBleCommandResult { Success = false, Outcome = outcome, });

        Assert.Null(result);
    }

    [Fact]
    public void CarThatWasNotHeardIsReportedAsNotFound()
    {
        var presence = CreatePresence(heard: false);

        var result = TeslaBleService.ClassifyPresence(presence, TestVin);

        Assert.Equal(BleConnectionTestResultType.CarNotFound, result);
    }

    [Fact]
    public void WarmingUpScanNeverDeclaresACarAway()
    {
        var presence = CreatePresence(heard: false);
        presence.WarmingUp = true;

        var result = TeslaBleService.ClassifyPresence(presence, TestVin);

        Assert.Null(result);
    }

    [Fact]
    public void HeardCarIsNarrowedDownFurther()
    {
        var presence = CreatePresence(heard: true);

        var result = TeslaBleService.ClassifyPresence(presence, TestVin);

        Assert.Null(result);
    }

    [Fact]
    public void StoppedScannerCarriesNoPresenceInformation()
    {
        var presence = CreatePresence(heard: false);
        presence.ScannerRunning = false;

        var result = TeslaBleService.ClassifyPresence(presence, TestVin);

        Assert.Equal(BleConnectionTestResultType.ContainerProblem, result);
    }

    [Fact]
    public void UnreachableContainerIsNoStatementAboutTheCar()
    {
        var presence = CreatePresence(heard: false);
        presence.ErrorMessage = "container not reachable";

        var result = TeslaBleService.ClassifyPresence(presence, TestVin);

        Assert.Equal(BleConnectionTestResultType.ContainerProblem, result);
    }

    [Fact]
    public void PresentCarWithoutBodyControllerAnswerMeansMissingKey()
    {
        var bodyControllerState = new DtoBleCommandResult
        {
            Success = false,
            Outcome = BleCommandOutcome.LinkFailed,
            ResultMessage = "failed to connect: vehicle rejected request: your public key has not been paired with the vehicle",
        };

        var result = TeslaBleService.ClassifyBodyControllerState(bodyControllerState, isAwake: false);

        Assert.Equal(BleConnectionTestResultType.KeyNotPaired, result);
    }

    [Fact]
    public void CarThatLeftBetweenTheRequestsIsNotBlamedOnTheKey()
    {
        var bodyControllerState = new DtoBleCommandResult { Success = false, Outcome = BleCommandOutcome.CarAbsent, };

        var result = TeslaBleService.ClassifyBodyControllerState(bodyControllerState, isAwake: false);

        Assert.Equal(BleConnectionTestResultType.CarNotFound, result);
    }

    [Fact]
    public void AnsweringBodyControllerOfASleepingCarMeansTheKeyWorks()
    {
        var bodyControllerState = new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, };

        var result = TeslaBleService.ClassifyBodyControllerState(bodyControllerState, isAwake: false);

        Assert.Equal(BleConnectionTestResultType.CarAsleep, result);
    }

    [Fact]
    public void AwakeCarWithWorkingKeyButFailedChargeStateStaysUnknown()
    {
        var bodyControllerState = new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, };

        var result = TeslaBleService.ClassifyBodyControllerState(bodyControllerState, isAwake: true);

        Assert.Equal(BleConnectionTestResultType.Unknown, result);
    }

    private static DtoBlePresenceResult CreatePresence(bool heard) => new()
    {
        ScannerRunning = true,
        Vehicles = new List<DtoBlePresenceVehicle>
        {
            new() { Vin = TestVin, Heard = heard, },
        },
    };
}
