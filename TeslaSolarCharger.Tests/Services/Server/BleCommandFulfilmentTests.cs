using Autofac;
using PkSoftwareService.Custom.Backend.Ble;
using TeslaSolarCharger.Shared.Resources;
using TeslaSolarCharger.Shared.Resources.Contracts;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

/// <summary>
/// Decides whether a BLE command counts as done or whether TSC falls back to the Fleet API. For cars without a Fleet
/// API license that fallback is rate limited to one command per hour, so a wrong answer here silently costs the user
/// their charging control.
/// </summary>
public class BleCommandFulfilmentTests : TestBase
{
    private static readonly IConstants RealConstants = new Constants();
    private static readonly string ChargeStartUrl = RealConstants.ChargeStartRequestUrl;
    private static readonly string ChargeStopUrl = RealConstants.ChargeStopRequestUrl;
    private static readonly string SetChargingAmpsUrl = RealConstants.SetChargingAmpsRequestUrl;

    public BleCommandFulfilmentTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    //The request urls come from the real Constants so the test can not pass against wrong urls.
    private TeslaSolarCharger.Server.Services.TeslaFleetApiService CreateService() =>
        Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>(
            new TypedParameter(typeof(IConstants), RealConstants));

    [Fact]
    public void SuccessIsAlwaysFulfilled()
    {
        var result = new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, };
        Assert.True(CreateService().IsBleCommandFulfilled(result,SetChargingAmpsUrl));
    }

    [Fact]
    public void RefusingChargeStartBecauseAlreadyChargingCountsAsFulfilled()
    {
        var result = new DtoBleCommandResult
        {
            Success = false,
            Outcome = BleCommandOutcome.CarRefused,
            CarErrorMessage = "is_charging",
        };
        Assert.True(CreateService().IsBleCommandFulfilled(result,ChargeStartUrl));
    }

    [Theory]
    [InlineData("requested", true)]
    [InlineData("no_power", true)]
    [InlineData(" requested ", true)]
    [InlineData("car could not execute command: no_power", true)]
    [InlineData("charge_requested_by_schedule", false)]
    [InlineData("requested_current_too_low", false)]
    [InlineData("xno_power", false)]
    [InlineData("no_power_available", false)]
    [InlineData("Requested", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void RefusingChargeStartForMissingPowerNeedsTheExactReason(string? carErrorMessage, bool expectedFulfilled)
    {
        var result = new DtoBleCommandResult
        {
            Success = false,
            Outcome = BleCommandOutcome.CarRefused,
            CarErrorMessage = carErrorMessage,
        };
        Assert.Equal(expectedFulfilled, CreateService().IsBleCommandFulfilled(result, ChargeStartUrl));
    }

    [Theory]
    [InlineData("requested")]
    [InlineData("no_power")]
    public void MissingPowerReasonOnOtherCommandIsNotFulfilled(string carErrorMessage)
    {
        var result = new DtoBleCommandResult
        {
            Success = false,
            Outcome = BleCommandOutcome.CarRefused,
            CarErrorMessage = carErrorMessage,
        };
        Assert.False(CreateService().IsBleCommandFulfilled(result, ChargeStopUrl));
    }

    [Fact]
    public void RefusingChargeStopBecauseNotChargingCountsAsFulfilled()
    {
        var result = new DtoBleCommandResult
        {
            Success = false,
            Outcome = BleCommandOutcome.CarRefused,
            CarErrorMessage = "not_charging",
        };
        Assert.True(CreateService().IsBleCommandFulfilled(result,ChargeStopUrl));
    }

    [Fact]
    public void OldContainerWithoutOutcomeStillWorksViaTheLegacyErrorType()
    {
        //BLE containers before 2.40.0 do not send an outcome. During a rollout window their results must keep working.
        var result = new DtoBleCommandResult
        {
            Success = false,
            Outcome = null,
            ErrorType = ErrorType.CarExecution,
            CarErrorMessage = "is_charging",
        };
        Assert.True(CreateService().IsBleCommandFulfilled(result,ChargeStartUrl));
    }

    [Fact]
    public void ARefusalForADifferentReasonIsNotFulfilled()
    {
        var result = new DtoBleCommandResult
        {
            Success = false,
            Outcome = BleCommandOutcome.CarRefused,
            CarErrorMessage = "invalid_command",
        };
        Assert.False(CreateService().IsBleCommandFulfilled(result,ChargeStartUrl));
    }

    [Fact]
    public void TheRefusalReasonMustMatchTheCommand()
    {
        //"not_charging" answers charging-stop, never charging-start.
        var result = new DtoBleCommandResult
        {
            Success = false,
            Outcome = BleCommandOutcome.CarRefused,
            CarErrorMessage = "not_charging",
        };
        Assert.False(CreateService().IsBleCommandFulfilled(result,ChargeStartUrl));
    }

    [Theory]
    [InlineData(BleCommandOutcome.CarAbsent)]
    [InlineData(BleCommandOutcome.LinkFailed)]
    [InlineData(BleCommandOutcome.CarAsleep)]
    [InlineData(BleCommandOutcome.AdapterUnavailable)]
    [InlineData(BleCommandOutcome.AdapterNotFound)]
    [InlineData(BleCommandOutcome.WorkerError)]
    [InlineData(BleCommandOutcome.WorkerTimeout)]
    public void NoOtherOutcomeCountsAsFulfilled(BleCommandOutcome outcome)
    {
        //Even with a matching message: only the car itself may declare the command unnecessary.
        var result = new DtoBleCommandResult { Success = false, Outcome = outcome, CarErrorMessage = "is_charging", };
        Assert.False(CreateService().IsBleCommandFulfilled(result,ChargeStartUrl));
    }

    [Fact]
    public void CommandsWithoutARefusalShortcutAreNeverFulfilledOnFailure()
    {
        var result = new DtoBleCommandResult
        {
            Success = false,
            Outcome = BleCommandOutcome.CarRefused,
            CarErrorMessage = "is_charging",
        };
        Assert.False(CreateService().IsBleCommandFulfilled(result,SetChargingAmpsUrl));
    }
}
