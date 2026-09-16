using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Dtos.ChargingCost.CostConfigurations;
using TeslaSolarCharger.Shared.Dtos.Setup;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.TimeProviding;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server.Setup;

public class SetupApplicationServiceTests : TestBase
{
    private readonly Mock<ISetupStateService> _setupStateService = new();
    private readonly Mock<IBaseConfigurationService> _baseConfigurationService = new();
    private readonly Mock<IConfigurationWrapper> _configurationWrapper = new();
    private readonly Mock<IChargingCostService> _chargingCostService = new();
    private readonly Mock<IConfigJsonService> _configJsonService = new();
    private readonly Mock<IDeferredSetupCheckService> _deferredSetupCheckService = new();
    private readonly Mock<ISetupDecisionService> _setupDecisionService = new();

    /// <summary>
    /// What the readiness checks make of the state. Finishing is held to this, so a test that wants it refused
    /// says so by making the configuration incomplete.
    /// </summary>
    private DtoSetupDecision _decision = new() { IsConfigurationComplete = true, };

    /// <summary>The configuration as it is on the installation right now, before the assistant writes anything.</summary>
    private DtoBaseConfiguration _liveConfiguration = new();

    /// <summary>Everything the assistant handed to the car saving service, in the order it did so.</summary>
    private readonly List<CarBasicConfiguration> _savedCarConfigurations = new();

    /// <summary>The order the application steps ran in, so a test can prove the base configuration went first.</summary>
    private readonly List<string> _operationOrder = new();

    /// <summary>
    /// The tariffs handed to the charging cost service, in order. Captured at call time rather than verified
    /// afterwards, so a later edit cannot make an earlier call look like it carried the newer value.
    /// </summary>
    private readonly List<DtoChargePrice> _savedChargePrices = new();

    public SetupApplicationServiceTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
        _configurationWrapper.Setup(w => w.GetBaseConfigurationAsync()).ReturnsAsync(() => _liveConfiguration);
        _setupDecisionService.Setup(s => s.Evaluate(It.IsAny<DtoSetupState>())).ReturnsAsync(() => _decision);
        _baseConfigurationService
            .Setup(s => s.UpdateBaseConfigurationAsync(It.IsAny<DtoBaseConfiguration>()))
            .Callback<DtoBaseConfiguration>(c =>
            {
                _operationOrder.Add("baseConfiguration");
                _liveConfiguration = c;
            })
            .Returns(Task.CompletedTask);
        _chargingCostService
            .Setup(s => s.UpdateChargePrice(It.IsAny<DtoChargePrice>()))
            .Callback<DtoChargePrice>(price =>
            {
                _operationOrder.Add("chargePrice");
                _savedChargePrices.Add(price);
            })
            .Returns(Task.CompletedTask);
        _configJsonService
            .Setup(s => s.UpdateCarBasicConfiguration(It.IsAny<int>(), It.IsAny<CarBasicConfiguration>()))
            .Callback<int, CarBasicConfiguration>((_, configuration) =>
            {
                _operationOrder.Add("car");
                _savedCarConfigurations.Add(configuration);
            })
            .Returns(Task.CompletedTask);
    }

    //The real validators, not mocks: what these tests have to prove is that a car cannot be switched on with
    //values the app itself rejects, and a mocked validator would agree to anything.
    private SetupApplicationService NewService() => new(
        Moq.Mock.Of<ILogger<SetupApplicationService>>(),
        _setupStateService.Object,
        _baseConfigurationService.Object,
        _configurationWrapper.Object,
        _chargingCostService.Object,
        _configJsonService.Object,
        Context,
        new FakeDateTimeProvider(CurrentFakeDate.UtcDateTime),
        _deferredSetupCheckService.Object,
        _setupDecisionService.Object,
        new CarBasicConfigurationValidator(),
        new BaseConfigurationValidator(),
        new DtoChargePriceValidator());

    /// <summary>
    /// A car that has a row but is not running yet - the normal state of a car being set up. Whether it was already
    /// managed decides whether saving may leave it switched off, so the two cases are kept apart deliberately.
    /// </summary>
    private static DtoSetupState StateWithOneCar(bool isAlreadyManaged = false) => new()
    {
        Configuration = new DtoBaseConfiguration { HomeGeofenceLatitude = 48.13, HomeGeofenceRadius = 120, },
        ChargePrice = new DtoChargePrice { GridPrice = 0.31m, SolarPrice = 0.1m, },
        CarDrafts =
        {
            new DtoSetupCarDraft
            {
                CarId = 5,
                WasManagedBeforeSetup = isAlreadyManaged,
                ConnectionRoute = SetupCarConnectionRoute.TeslaCloud,
                Configuration = new CarBasicConfiguration
                {
                    Id = 5, Name = "Car", Vin = "VIN1", UsableEnergy = 75, MaximumPhases = 3,
                    MinimumAmpere = 6, MaximumAmpere = 16, ChargingPriority = 1,
                    ShouldBeManaged = isAlreadyManaged,
                },
            },
        },
    };

    [Fact]
    public async Task BaseConfigurationIsSavedBeforeAnyCar()
    {
        //The installation wide switches a car's own validation reads have to be in place before the car is saved.
        await NewService().ApplyConfiguration(StateWithOneCar());

        Assert.Equal("baseConfiguration", _operationOrder.First());
        Assert.Equal(new[] { "baseConfiguration", "chargePrice", "car", }, _operationOrder);
    }

    [Fact]
    public async Task SavingDuringSetupNeverHandsANewCarToTheChargingScheduler()
    {
        //Activation is its own explicit step, so a car that is not running yet stays switched off however complete
        //its configuration looks.
        await NewService().ApplyConfiguration(StateWithOneCar());

        Assert.False(Assert.Single(_savedCarConfigurations).ShouldBeManaged);
    }

    [Fact]
    public async Task SavingDuringSetupDoesNotSwitchOffACarThatIsAlreadyCharging()
    {
        //Reopening the assistant to add a second car must not stop the first one. Saving it unmanaged would, and
        //would also make the real ConfigJsonService drop every charging connector it is allowed on.
        await NewService().ApplyConfiguration(StateWithOneCar(isAlreadyManaged: true));

        Assert.True(Assert.Single(_savedCarConfigurations).ShouldBeManaged);
    }

    [Fact]
    public async Task ApplyingDoesNotEditTheDraftTheUserIsStillWorkingOn()
    {
        var state = StateWithOneCar();
        state.CarDrafts[0].Configuration.ShouldBeManaged = true;

        await NewService().ApplyConfiguration(state);

        //The car was written switched off, but the draft on screen is untouched.
        Assert.False(Assert.Single(_savedCarConfigurations).ShouldBeManaged);
        Assert.True(state.CarDrafts[0].Configuration.ShouldBeManaged);
    }

    [Fact]
    public async Task ApplyingDoesNotFinishSetup()
    {
        _liveConfiguration = new DtoBaseConfiguration { IsFirstRun = true, };

        var result = await NewService().ApplyConfiguration(StateWithOneCar());

        Assert.True(result.IsSuccess);
        Assert.False(result.IsSetupCompleted);
        Assert.True(_liveConfiguration.IsFirstRun);
        _setupStateService.Verify(s => s.DeleteSetupState(), Times.Never);
    }

    [Fact]
    public async Task OnlyTheSettingsTheAssistantOwnsAreWritten()
    {
        //Something changed in Base Configuration while the assistant was open must survive being applied.
        _liveConfiguration = new DtoBaseConfiguration { PowerBuffer = 500, MqttClientIdPrefix = "OTHER-", HomeGeofenceRadius = 50, };

        await NewService().ApplyConfiguration(StateWithOneCar());

        Assert.Equal(500, _liveConfiguration.PowerBuffer);
        Assert.Equal("OTHER-", _liveConfiguration.MqttClientIdPrefix);
        //The geofence is the assistant's to set, so its value wins.
        Assert.Equal(120, _liveConfiguration.HomeGeofenceRadius);
        Assert.Equal(48.13, _liveConfiguration.HomeGeofenceLatitude);
    }

    [Fact]
    public async Task AFailedBaseConfigurationSaveStopsEverythingElse()
    {
        _baseConfigurationService
            .Setup(s => s.UpdateBaseConfigurationAsync(It.IsAny<DtoBaseConfiguration>()))
            .ThrowsAsync(new InvalidOperationException("database is gone"));

        var result = await NewService().ApplyConfiguration(StateWithOneCar());

        Assert.False(result.IsSuccess);
        Assert.Empty(_savedCarConfigurations);
        var failure = Assert.Single(result.FailedOperations);
        Assert.Equal(SetupOperationKey.SaveBaseConfiguration, failure.OperationKey);
        Assert.Equal("database is gone", failure.ErrorMessage);
        Assert.True(failure.IsRetryable);
    }

    [Fact]
    public async Task AFailedPriceSaveIsReportedInsteadOfPassingSilently()
    {
        _chargingCostService
            .Setup(s => s.UpdateChargePrice(It.IsAny<DtoChargePrice>()))
            .ThrowsAsync(new InvalidOperationException("price rejected"));

        var result = await NewService().ApplyConfiguration(StateWithOneCar());

        Assert.False(result.IsSuccess);
        Assert.Contains(result.FailedOperations, o => o.OperationKey == SetupOperationKey.SaveChargePrice);
    }

    [Fact]
    public async Task AValidationFailureIsNotOfferedAsSomethingToJustRetry()
    {
        _configJsonService
            .Setup(s => s.UpdateCarBasicConfiguration(It.IsAny<int>(), It.IsAny<CarBasicConfiguration>()))
            .ThrowsAsync(new FluentValidation.ValidationException("Vin must not be empty"));

        var result = await NewService().ApplyConfiguration(StateWithOneCar());

        var failure = Assert.Single(result.FailedOperations);
        Assert.False(failure.IsRetryable);
    }

    [Fact]
    public async Task AFailedSaveKeepsSetupUnfinishedAndItsAnswersIntact()
    {
        _liveConfiguration = new DtoBaseConfiguration { IsFirstRun = true, };
        _configJsonService
            .Setup(s => s.UpdateCarBasicConfiguration(It.IsAny<int>(), It.IsAny<CarBasicConfiguration>()))
            .ThrowsAsync(new InvalidOperationException("car rejected"));

        var result = await NewService().ActivateAndCompleteSetup(StateWithOneCar());

        Assert.False(result.IsSetupCompleted);
        Assert.True(_liveConfiguration.IsFirstRun);
        _setupStateService.Verify(s => s.DeleteSetupState(), Times.Never);
    }

    [Fact]
    public async Task FinishingSuccessfullyActivatesTheCarAndMarksSetupComplete()
    {
        _liveConfiguration = new DtoBaseConfiguration { IsFirstRun = true, };

        var result = await NewService().ActivateAndCompleteSetup(StateWithOneCar());

        Assert.True(result.IsSuccess);
        Assert.True(result.IsSetupCompleted);
        Assert.False(_liveConfiguration.IsFirstRun);
        //Saved once switched off while applying, then again switched on while activating.
        Assert.Equal(2, _savedCarConfigurations.Count);
        Assert.False(_savedCarConfigurations[0].ShouldBeManaged);
        Assert.True(_savedCarConfigurations[1].ShouldBeManaged);
        _setupStateService.Verify(s => s.DeleteSetupState(), Times.Once);
    }

    [Fact]
    public async Task FinishingSwitchesOnEveryCarInSetup()
    {
        //There is no switch to leave a car off any more. A car the user does not want controlled is taken out of
        //setup instead, so every car still in it - one that was already in the database but not running included -
        //is switched on.
        var state = StateWithOneCar();
        state.CarDrafts.Add(new DtoSetupCarDraft
        {
            CarId = 6,
            WasManagedBeforeSetup = false,
            ConnectionRoute = SetupCarConnectionRoute.TeslaCloud,
            Configuration = new CarBasicConfiguration
            {
                Id = 6, Name = "Second car", Vin = "VIN2", UsableEnergy = 60, MaximumPhases = 3,
                MinimumAmpere = 6, MaximumAmpere = 16, ChargingPriority = 2, ShouldBeManaged = false,
            },
        });

        var result = await NewService().ActivateAndCompleteSetup(state);

        Assert.True(result.IsSetupCompleted);
        var activated = _savedCarConfigurations.Where(c => c.ShouldBeManaged).Select(c => c.Id).ToList();
        Assert.Equal(new List<int> { 5, 6, }, activated);
    }

    [Fact]
    public async Task RetryingSkipsWhatAlreadyWorked()
    {
        var state = StateWithOneCar();
        var service = NewService();
        _chargingCostService
            .Setup(s => s.UpdateChargePrice(It.IsAny<DtoChargePrice>()))
            .ThrowsAsync(new InvalidOperationException("price rejected"));

        var firstAttempt = await service.ApplyConfiguration(state);
        Assert.False(firstAttempt.IsSuccess);

        //The price service recovers; the base configuration must not be written a second time.
        _chargingCostService
            .Setup(s => s.UpdateChargePrice(It.IsAny<DtoChargePrice>()))
            .Callback<DtoChargePrice>(price =>
            {
                _operationOrder.Add("chargePrice");
                _savedChargePrices.Add(price);
            })
            .Returns(Task.CompletedTask);

        var secondAttempt = await service.ApplyConfiguration(state);

        Assert.True(secondAttempt.IsSuccess);
        Assert.True(secondAttempt.Operations.Single(o => o.OperationKey == SetupOperationKey.SaveBaseConfiguration).WasAlreadyCompleted);
        Assert.Equal(1, _operationOrder.Count(o => o == "baseConfiguration"));
    }

    [Fact]
    public async Task ProgressIsRecordedAsItHappensSoAnInterruptionDoesNotRepeatWork()
    {
        var state = StateWithOneCar();

        await NewService().ApplyConfiguration(state);

        Assert.Contains(state.CompletedOperations, o => o.OperationKey == SetupOperationKey.SaveBaseConfiguration);
        Assert.Contains(state.CompletedOperations, o => o.OperationKey == SetupOperationKey.SaveCarDraft);
        _setupStateService.Verify(s => s.UpdateSetupState(state), Times.AtLeast(3));
    }

    [Fact]
    public async Task ANewCarIsLookedUpAfterSavingSoARetryUpdatesItInsteadOfCreatingASecond()
    {
        Context.Cars.Add(new Car { Id = 11, Vin = "NEWVIN", });
        await Context.SaveChangesAsync();
        var state = StateWithOneCar();
        state.CarDrafts[0].CarId = null;
        state.CarDrafts[0].Configuration.Vin = "NEWVIN";

        await NewService().ApplyConfiguration(state);

        Assert.Equal(11, state.CarDrafts[0].CarId);
    }

    [Fact]
    public async Task ConnectorAssignmentsMadeWhileTheCarWasADraftAreAppliedOnActivation()
    {
        Context.Cars.Add(new Car { Id = 5, Vin = "VIN1", });
        Context.OcppChargingStationConnectors.Add(new OcppChargingStationConnector("Connector 1") { Id = 3, OcppChargingStationId = 1, });
        await Context.SaveChangesAsync();
        var state = StateWithOneCar();
        state.CarDrafts[0].AssignedChargingConnectorIds.Add(3);

        await NewService().ActivateAndCompleteSetup(state);

        var assignment = Assert.Single(await Context.ChargingStationConnectorAllowedCars.ToListAsync());
        Assert.Equal(5, assignment.CarId);
        Assert.Equal(3, assignment.OcppChargingStationConnectorId);
    }

    [Fact]
    public async Task ActivatingTheSameAssignmentTwiceDoesNotDuplicateIt()
    {
        Context.Cars.Add(new Car { Id = 5, Vin = "VIN1", });
        Context.OcppChargingStationConnectors.Add(new OcppChargingStationConnector("Connector 1") { Id = 3, OcppChargingStationId = 1, });
        Context.ChargingStationConnectorAllowedCars.Add(new ChargingStationConnectorAllowedCar { CarId = 5, OcppChargingStationConnectorId = 3, });
        await Context.SaveChangesAsync();
        DetachAllEntities();
        var state = StateWithOneCar();
        state.CarDrafts[0].AssignedChargingConnectorIds.Add(3);

        await NewService().ActivateAndCompleteSetup(state);

        Assert.Single(await Context.ChargingStationConnectorAllowedCars.ToListAsync());
    }

    [Fact]
    public async Task ActivatingAConnectorMakesItManaged()
    {
        Context.OcppChargingStationConnectors.Add(new OcppChargingStationConnector("Connector 1") { Id = 7, OcppChargingStationId = 1, ShouldBeManaged = false, });
        await Context.SaveChangesAsync();
        DetachAllEntities();
        var state = StateWithOneCar();
        state.CarDrafts.Clear();
        state.ChargerDrafts.Add(new DtoSetupChargerDraft { ConnectorId = 7, AllowGuestCars = true, });

        var result = await NewService().ActivateAndCompleteSetup(state);

        Assert.True(result.IsSetupCompleted);
        var connector = await Context.OcppChargingStationConnectors.FirstAsync(c => c.Id == 7);
        Assert.True(connector.ShouldBeManaged);
        Assert.True(connector.AllowGuestCars);
    }

    [Fact]
    public async Task ActivatingAConnectorWithoutALowestCurrentUsesSixAmps()
    {
        //Setup does not ask for it, but a managed connector cannot do without one.
        Context.OcppChargingStationConnectors.Add(new OcppChargingStationConnector("Connector 1") { Id = 7, OcppChargingStationId = 1, MinCurrent = null, });
        await Context.SaveChangesAsync();
        DetachAllEntities();
        var state = StateWithOneCar();
        state.CarDrafts.Clear();
        state.ChargerDrafts.Add(new DtoSetupChargerDraft { ConnectorId = 7, });

        await NewService().ActivateAndCompleteSetup(state);

        var connector = await Context.OcppChargingStationConnectors.FirstAsync(c => c.Id == 7);
        Assert.Equal(SetupApplicationService.DefaultChargingStationMinCurrent, connector.MinCurrent);
        Assert.Equal(6, connector.MinCurrent);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public async Task ActivatingAConnectorKeepsALowestCurrentItAlreadyHas(int minCurrent)
    {
        Context.OcppChargingStationConnectors.Add(new OcppChargingStationConnector("Connector 1") { Id = 7, OcppChargingStationId = 1, MinCurrent = minCurrent, });
        await Context.SaveChangesAsync();
        DetachAllEntities();
        var state = StateWithOneCar();
        state.CarDrafts.Clear();
        state.ChargerDrafts.Add(new DtoSetupChargerDraft { ConnectorId = 7, });

        await NewService().ActivateAndCompleteSetup(state);

        var connector = await Context.OcppChargingStationConnectors.FirstAsync(c => c.Id == 7);
        Assert.Equal(minCurrent, connector.MinCurrent);
    }

    [Fact]
    public async Task FinishingNotesThatTheCarHasNotBeenSeenChargingYet()
    {
        var state = StateWithOneCar();

        await NewService().ActivateAndCompleteSetup(state);

        _deferredSetupCheckService.Verify(s => s.AddOrUpdateDeferredCheck(It.Is<DtoDeferredSetupCheck>(
            c => c.Kind == DeferredSetupCheckKind.RealChargingTest
                 && c.DeviceKind == SetupDeviceKind.Car
                 && c.DeviceId == 5)), Times.Once);
    }

    [Fact]
    public async Task TheNoteRemembersHowTheCarIsControlled()
    {
        var state = StateWithOneCar();
        state.CarDrafts[0].ConnectionRoute = SetupCarConnectionRoute.TeslaBluetooth;
        state.CarDrafts[0].Configuration.UseBle = true;
        state.CarDrafts[0].Configuration.BleApiBaseUrl = "http://ble";

        await NewService().ActivateAndCompleteSetup(state);

        //Recorded so that changing how the car is reached makes an earlier result stop counting.
        _deferredSetupCheckService.Verify(s => s.AddOrUpdateDeferredCheck(It.Is<DtoDeferredSetupCheck>(
            c => c.ConfigurationFingerprint != null
                 && c.ConfigurationFingerprint.Contains(nameof(SetupCarConnectionRoute.TeslaBluetooth))
                 && c.ConfigurationFingerprint.Contains("http://ble"))), Times.Once);
    }

    [Fact]
    public async Task ANoteThatCannotBeWrittenDoesNotFailAnOtherwiseGoodSetup()
    {
        _deferredSetupCheckService
            .Setup(s => s.AddOrUpdateDeferredCheck(It.IsAny<DtoDeferredSetupCheck>()))
            .ThrowsAsync(new InvalidOperationException("storage is gone"));

        var result = await NewService().ActivateAndCompleteSetup(StateWithOneCar());

        Assert.True(result.IsSetupCompleted);
    }

    [Fact]
    public async Task AFailedFinishNotesNothing()
    {
        _configJsonService
            .Setup(s => s.UpdateCarBasicConfiguration(It.IsAny<int>(), It.IsAny<CarBasicConfiguration>()))
            .ThrowsAsync(new InvalidOperationException("car rejected"));

        await NewService().ActivateAndCompleteSetup(StateWithOneCar());

        //Setup did not finish, so nothing was switched on and there is nothing outstanding to watch for.
        _deferredSetupCheckService.Verify(s => s.AddOrUpdateDeferredCheck(It.IsAny<DtoDeferredSetupCheck>()), Times.Never);
    }

    [Fact]
    public async Task SavingOneCarOnItsOwnLeavesItSwitchedOff()
    {
        var state = StateWithOneCar();

        var result = await NewService().SaveCarDraft(state, state.CarDrafts[0].DraftId);

        Assert.True(result.IsSuccess);
        //It now has a row to hang settings and tests off, but the scheduler must still not see it.
        Assert.False(Assert.Single(_savedCarConfigurations).ShouldBeManaged);
    }

    [Fact]
    public async Task SavingOneCarAgainActuallySavesItAgain()
    {
        var state = StateWithOneCar();
        var service = NewService();
        await service.SaveCarDraft(state, state.CarDrafts[0].DraftId);

        state.CarDrafts[0].Configuration.UsableEnergy = 90;
        await service.SaveCarDraft(state, state.CarDrafts[0].DraftId);

        //An explicit save follows an edit, so a record of the previous one must not turn it into a no-op.
        Assert.Equal(2, _savedCarConfigurations.Count);
        Assert.Equal(90, _savedCarConfigurations[1].UsableEnergy);
    }

    [Fact]
    public async Task SavingOneCarFillsInTheRowItWasWrittenTo()
    {
        Context.Cars.Add(new Car { Id = 11, Vin = "VIN1", });
        await Context.SaveChangesAsync();
        var state = StateWithOneCar();
        state.CarDrafts[0].CarId = null;

        await NewService().SaveCarDraft(state, state.CarDrafts[0].DraftId);

        Assert.Equal(11, state.CarDrafts[0].CarId);
    }

    [Fact]
    public async Task SavingACarThatIsNoLongerPartOfSetupIsReportedRatherThanIgnored()
    {
        var result = await NewService().SaveCarDraft(StateWithOneCar(), Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(SetupOperationKey.SaveCarDraft, Assert.Single(result.FailedOperations).OperationKey);
    }

    [Fact]
    public void AcceptedProposalsAreWrittenToTheStateAndMarkedAsDecidedByTheApp()
    {
        var state = StateWithOneCar();
        var proposals = new List<DtoSetupProposedValue>
        {
            new()
            {
                PropertyName = nameof(BaseConfigurationBase.DynamicHomeBatteryMinSoc), Value = true,
                Source = SetupValueSource.DerivedFromAnswers,
            },
        };

        var updated = NewService().AcceptProposals(state, proposals);

        Assert.True(updated.Configuration.DynamicHomeBatteryMinSoc);
        Assert.Equal(SetupValueSource.DerivedFromAnswers, updated.ValueSources[nameof(BaseConfigurationBase.DynamicHomeBatteryMinSoc)]);
    }

    [Fact]
    public void AcceptedProposalsForACarReachThatCarsDraft()
    {
        var state = StateWithOneCar();
        var proposals = new List<DtoSetupProposedValue>
        {
            new()
            {
                PropertyName = nameof(CarBasicConfiguration.HomeDetectionVia),
                //Travelling through JSON turns an enum into its name, which is what the server actually receives.
                Value = nameof(HomeDetectionVia.BlePresence),
                DraftId = state.CarDrafts[0].DraftId,
            },
        };

        var updated = NewService().AcceptProposals(state, proposals);

        Assert.Equal(HomeDetectionVia.BlePresence, updated.CarDrafts[0].Configuration.HomeDetectionVia);
    }

    [Fact]
    public void AProposalForAPropertyThatDoesNotExistIsIgnoredInsteadOfFailingTheSave()
    {
        var state = StateWithOneCar();
        var proposals = new List<DtoSetupProposedValue> { new() { PropertyName = "NoSuchSetting", Value = true, }, };

        var updated = NewService().AcceptProposals(state, proposals);

        Assert.DoesNotContain("NoSuchSetting", updated.ValueSources.Keys);
    }

    [Fact]
    public void AProposalForACarThatIsNoLongerInTheStateIsIgnored()
    {
        var state = StateWithOneCar();
        var proposals = new List<DtoSetupProposedValue>
        {
            new() { PropertyName = nameof(CarBasicConfiguration.ChargingPriority), Value = 3, DraftId = Guid.NewGuid(), },
        };

        var updated = NewService().AcceptProposals(state, proposals);

        Assert.Equal(1, updated.CarDrafts[0].Configuration.ChargingPriority);
    }

    [Theory]
    [InlineData(nameof(CarBasicConfiguration.UsableEnergy))]
    [InlineData(nameof(CarBasicConfiguration.MaximumPhases))]
    public async Task ACarWithMissingSpecificationsCannotBeSwitchedOn(string missingProperty)
    {
        //The detailed settings pages are validated by the controller before they reach the service. Setup posts a
        //whole state instead, so without validating here it is the one route that can start charging a car on
        //values the app itself refuses.
        var state = StateWithOneCar();
        typeof(CarBasicConfiguration).GetProperty(missingProperty)!.SetValue(state.CarDrafts[0].Configuration, 0);

        var result = await NewService().ActivateAndCompleteSetup(state);

        Assert.False(result.IsSetupCompleted);
        var failure = Assert.Single(result.FailedOperations);
        Assert.Equal(SetupOperationKey.ActivateCar, failure.OperationKey);
        //Something to change, not something to try again unchanged.
        Assert.False(failure.IsRetryable);
    }

    [Fact]
    public async Task AnIncompleteCarCanStillBeSavedAsADraft()
    {
        //Saving early is what gives a connection test something real to work with. It stays switched off, so the
        //values that only matter for charging do not have to be answered yet.
        var state = StateWithOneCar();
        state.CarDrafts[0].Configuration.UsableEnergy = 0;

        var result = await NewService().SaveCarDraft(state, state.CarDrafts[0].DraftId);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ABaseConfigurationTheAppWouldRefuseIsNotWritten()
    {
        //Working out the battery reserve automatically needs the solar forecast; storing one without the other
        //leaves the installation in a state its own validator rejects.
        var state = StateWithOneCar();
        state.Configuration.DynamicHomeBatteryMinSoc = true;
        state.Configuration.PredictSolarPowerGeneration = false;
        state.Configuration.HomeBatteryUsableEnergy = 10;
        state.Configuration.HomeBatteryChargingPower = 3000;

        var result = await NewService().ApplyConfiguration(state);

        Assert.False(result.IsSuccess);
        var failure = Assert.Single(result.FailedOperations);
        Assert.Equal(SetupOperationKey.SaveBaseConfiguration, failure.OperationKey);
        Assert.Empty(_savedCarConfigurations);
    }

    [Fact]
    public async Task ANewCarsIdIsCarriedOnItsConfigurationAndNotOnlyOnTheDraft()
    {
        //The configuration is handed straight to the in memory car on the next save. A zero there renumbers a car
        //that is already running, and every later lookup by its real id then fails.
        Context.Cars.Add(new Car { Id = 11, Vin = "NEWVIN", });
        await Context.SaveChangesAsync();
        var state = StateWithOneCar();
        state.CarDrafts[0].CarId = null;
        state.CarDrafts[0].Configuration.Id = 0;
        state.CarDrafts[0].Configuration.Vin = "NEWVIN";

        await NewService().ApplyConfiguration(state);

        Assert.Equal(11, state.CarDrafts[0].CarId);
        Assert.Equal(11, state.CarDrafts[0].Configuration.Id);
    }

    [Fact]
    public async Task TimeOfUsePeriodsReachTheStoredTariff()
    {
        var state = StateWithOneCar();
        state.ElectricityPriceKind = SetupElectricityPriceKind.TimeOfUse;
        state.FixedPrices.Add(new FixedPrice { FromHour = 22, ToHour = 6, Value = 0.19m, });

        await NewService().ApplyConfiguration(state);

        //The periods are edited as a list but stored as the price's serialized configuration; without this the
        //tariff the user described is simply lost.
        var saved = Assert.Single(_savedChargePrices);
        Assert.NotNull(saved.EnergyProviderConfiguration);
        Assert.Contains("0.19", saved.EnergyProviderConfiguration);
    }

    [Fact]
    public async Task ChangingAwayFromATimeOfUseTariffClearsTheOldPeriods()
    {
        var state = StateWithOneCar();
        state.ElectricityPriceKind = SetupElectricityPriceKind.Fixed;
        state.ChargePrice!.EnergyProviderConfiguration = "[{\"FromHour\":22,\"Value\":0.19}]";

        await NewService().ApplyConfiguration(state);

        //Otherwise yesterday's periods stay in force behind a tariff that no longer has any.
        Assert.Null(Assert.Single(_savedChargePrices).EnergyProviderConfiguration);
    }

    [Fact]
    public async Task ATariffNobodyHasAnsweredIsNotStored()
    {
        var state = StateWithOneCar();
        state.ChargePrice!.GridPrice = null;

        var result = await NewService().ApplyConfiguration(state);

        Assert.True(result.IsSuccess);
        _chargingCostService.Verify(s => s.UpdateChargePrice(It.IsAny<DtoChargePrice>()), Times.Never);
    }

    [Fact]
    public async Task AHouseholdWithoutPanelsGetsAnExportValueRatherThanAFailedSave()
    {
        var state = StateWithOneCar();
        state.ChargePrice!.SolarPrice = null;

        var result = await NewService().ApplyConfiguration(state);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, Assert.Single(_savedChargePrices).SolarPrice);
    }

    [Fact]
    public async Task EditingAValueAfterSavingWritesItInsteadOfSkippingTheStep()
    {
        //Save without enabling, come back, change the price, finish. The earlier record must not make the new
        //answer a no-op, or the value the user replaced stays in force.
        var state = StateWithOneCar();
        var service = NewService();
        await service.ApplyConfiguration(state);
        Assert.Equal(1, _operationOrder.Count(o => o == "chargePrice"));

        state.ChargePrice!.GridPrice = 0.42m;
        await service.ApplyConfiguration(state);

        Assert.Equal(2, _operationOrder.Count(o => o == "chargePrice"));
        Assert.Equal(0.42m, _savedChargePrices.Last().GridPrice);
    }

    [Theory]
    [InlineData(SetupElectricityPriceKind.Fixed)]
    [InlineData(SetupElectricityPriceKind.TimeOfUse)]
    public async Task ApplyingTheSameAnswersTwiceStillDoesNotRepeatTheWork(SetupElectricityPriceKind kind)
    {
        //Including a time of use tariff, whose periods are stored in a shape the answers themselves never take on.
        //Deriving that onto the state instead of onto a copy would make every apply look like a fresh edit.
        var state = StateWithOneCar();
        state.ElectricityPriceKind = kind;
        state.FixedPrices.Add(new FixedPrice { FromHour = 22, ToHour = 6, Value = 0.19m, });
        var service = NewService();
        await service.ApplyConfiguration(state);

        var secondAttempt = await service.ApplyConfiguration(state);

        Assert.True(secondAttempt.Operations.Single(o => o.OperationKey == SetupOperationKey.SaveChargePrice).WasAlreadyCompleted);
        Assert.Equal(1, _operationOrder.Count(o => o == "chargePrice"));
    }

    [Fact]
    public async Task SavingANewCarTwiceStillLeavesItSwitchedOff()
    {
        //A fresh CarBasicConfiguration says it should be managed from the moment it is constructed, so deriving
        //"was already running" from it made the second save of a car the user had just added switch it on.
        var state = StateWithOneCar();
        state.CarDrafts[0].WasManagedBeforeSetup = false;
        state.CarDrafts[0].Configuration.ShouldBeManaged = true;
        var service = NewService();

        await service.SaveCarDraft(state, state.CarDrafts[0].DraftId);
        await service.SaveCarDraft(state, state.CarDrafts[0].DraftId);

        Assert.Equal(2, _savedCarConfigurations.Count);
        Assert.All(_savedCarConfigurations, c => Assert.False(c.ShouldBeManaged));
    }

    [Fact]
    public async Task TheFirstCarGetsTheFirstPlace()
    {
        var state = StateWithOneCar();
        state.CarDrafts[0].Configuration.ChargingPriority = 0;

        await NewService().SaveCarDraft(state, state.CarDrafts[0].DraftId);

        Assert.Equal(1, Assert.Single(_savedCarConfigurations).ChargingPriority);
        Assert.Equal(1, state.CarDrafts[0].Configuration.ChargingPriority);
    }

    [Fact]
    public async Task ANewCarGetsThePlaceAfterEveryOtherCar()
    {
        //Cars are served in the order they were set up, and the order is never asked for.
        Context.Cars.Add(new Car { Id = 1, Vin = "OTHER1", ChargingPriority = 1, });
        Context.Cars.Add(new Car { Id = 2, Vin = "OTHER2", ChargingPriority = 4, });
        await Context.SaveChangesAsync();
        DetachAllEntities();
        var state = StateWithOneCar();
        state.CarDrafts[0].CarId = null;
        state.CarDrafts[0].Configuration.Id = 0;
        state.CarDrafts[0].Configuration.ChargingPriority = 0;

        await NewService().SaveCarDraft(state, state.CarDrafts[0].DraftId);

        Assert.Equal(5, Assert.Single(_savedCarConfigurations).ChargingPriority);
        Assert.Equal(5, state.CarDrafts[0].Configuration.ChargingPriority);
    }

    [Fact]
    public async Task ACarKeepsThePlaceItsRowAlreadyHas()
    {
        //A browser that did not pick up the number from the first save posts zero again. Counting on from the other
        //cars then would move this car behind every car added since.
        Context.Cars.Add(new Car { Id = 5, Vin = "VIN1", ChargingPriority = 2, });
        Context.Cars.Add(new Car { Id = 6, Vin = "OTHER", ChargingPriority = 3, });
        await Context.SaveChangesAsync();
        DetachAllEntities();
        var state = StateWithOneCar();
        state.CarDrafts[0].Configuration.ChargingPriority = 0;

        await NewService().SaveCarDraft(state, state.CarDrafts[0].DraftId);

        Assert.Equal(2, Assert.Single(_savedCarConfigurations).ChargingPriority);
    }

    [Fact]
    public async Task ACarWithAPlaceKeepsIt()
    {
        Context.Cars.Add(new Car { Id = 6, Vin = "OTHER", ChargingPriority = 9, });
        await Context.SaveChangesAsync();
        DetachAllEntities();
        var state = StateWithOneCar();
        state.CarDrafts[0].Configuration.ChargingPriority = 3;

        await NewService().SaveCarDraft(state, state.CarDrafts[0].DraftId);

        Assert.Equal(3, Assert.Single(_savedCarConfigurations).ChargingPriority);
    }

    [Fact]
    public async Task ACarWithoutAPlaceCanStillBeSwitchedOn()
    {
        //The car validator refuses a managed car without a priority, and nobody is asked for one any more.
        var state = StateWithOneCar();
        state.CarDrafts[0].Configuration.ChargingPriority = 0;

        var result = await NewService().ActivateAndCompleteSetup(state);

        Assert.True(result.IsSetupCompleted);
        Assert.All(_savedCarConfigurations, c => Assert.Equal(1, c.ChargingPriority));
        Assert.True(_savedCarConfigurations.Last().ShouldBeManaged);
    }

    [Fact]
    public async Task AnImportedTimeOfUseTariffKeepsItsPeriodsWhenTheKindWasNeverReAnswered()
    {
        //Loading an existing tariff restores its periods but leaves the tariff kind unanswered - the screen reads
        //it back from the periods. The server has to read it the same way, or saving strips what it is showing.
        var state = StateWithOneCar();
        state.ElectricityPriceKind = null;
        state.FixedPrices.Add(new FixedPrice { FromHour = 22, ToHour = 6, Value = 0.19m, });

        await NewService().ApplyConfiguration(state);

        var saved = Assert.Single(_savedChargePrices);
        Assert.NotNull(saved.EnergyProviderConfiguration);
        Assert.Contains("0.19", saved.EnergyProviderConfiguration);
    }

    [Fact]
    public async Task AMarketTariffWithNoRegionIsNotStored()
    {
        //Market prices are published per region, so there is nothing to fetch without one - which is why the app's
        //own tariff validator refuses it. Setup used to be the one route that could store it anyway.
        var state = StateWithOneCar();
        state.ElectricityPriceKind = SetupElectricityPriceKind.Market;
        state.ChargePrice!.AddSpotPriceToGridPrice = true;
        state.ChargePrice.SpotPriceRegion = null;

        var result = await NewService().ApplyConfiguration(state);

        Assert.False(result.IsSuccess);
        var failure = Assert.Single(result.FailedOperations);
        Assert.Equal(SetupOperationKey.SaveChargePrice, failure.OperationKey);
        Assert.False(failure.IsRetryable);
        Assert.Empty(_savedChargePrices);
    }

    [Fact]
    public async Task SetupCannotBeFinishedWhileTheReadinessChecksSayItIsNotReady()
    {
        //Gating the button in the browser is not the same as a rule: a stale tab or anything else posting this
        //state would otherwise still switch equipment on that the app itself reports as not ready.
        _liveConfiguration = new DtoBaseConfiguration { IsFirstRun = true, };
        _decision = new DtoSetupDecision
        {
            IsConfigurationComplete = false,
            MissingInformation = { new DtoSetupIssue { MessageKey = "SetupIssueGridPriceMissing", }, },
        };

        var result = await NewService().ActivateAndCompleteSetup(StateWithOneCar());

        Assert.False(result.IsSetupCompleted);
        Assert.False(Assert.Single(result.FailedOperations).IsRetryable);
        //Nothing was written at all, so the user loses nothing by being sent back.
        Assert.Empty(_savedCarConfigurations);
        Assert.True(_liveConfiguration.IsFirstRun);
        _setupStateService.Verify(s => s.DeleteSetupState(), Times.Never);
    }

    [Fact]
    public async Task AChargerWithNoConnectorChosenFailsInsteadOfBeingSkipped()
    {
        //It used to be filtered out of activation while setup still completed, leaving the user with a charger
        //they believe is switched on.
        var state = StateWithOneCar();
        state.ChargerDrafts.Add(new DtoSetupChargerDraft
        {
            ChargepointId = "CP1", ChargingStationId = 2, ConnectorId = null,
        });

        var result = await NewService().ActivateAndCompleteSetup(state);

        Assert.False(result.IsSetupCompleted);
        Assert.Contains(result.FailedOperations, o => o.OperationKey == SetupOperationKey.ActivateChargingStationConnector);
        _setupStateService.Verify(s => s.DeleteSetupState(), Times.Never);
    }

    [Fact]
    public async Task UntickingAChargingConnectorActuallyRemovesTheCarFromIt()
    {
        //Only adding would leave the car charging somewhere the user has just said it does not belong.
        Context.Cars.Add(new Car { Id = 5, Vin = "VIN1", });
        Context.OcppChargingStationConnectors.Add(new OcppChargingStationConnector("Connector 1") { Id = 3, OcppChargingStationId = 1, });
        Context.OcppChargingStationConnectors.Add(new OcppChargingStationConnector("Connector 2") { Id = 4, OcppChargingStationId = 1, });
        Context.ChargingStationConnectorAllowedCars.Add(new ChargingStationConnectorAllowedCar { CarId = 5, OcppChargingStationConnectorId = 3, });
        Context.ChargingStationConnectorAllowedCars.Add(new ChargingStationConnectorAllowedCar { CarId = 5, OcppChargingStationConnectorId = 4, });
        await Context.SaveChangesAsync();
        DetachAllEntities();
        var state = StateWithOneCar();
        state.CarDrafts[0].AssignedChargingConnectorIds.Add(4);

        await NewService().ActivateAndCompleteSetup(state);

        var assignment = Assert.Single(await Context.ChargingStationConnectorAllowedCars.ToListAsync());
        Assert.Equal(4, assignment.OcppChargingStationConnectorId);
    }
}
