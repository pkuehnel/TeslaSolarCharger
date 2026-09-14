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

    /// <summary>The configuration as it is on the installation right now, before the assistant writes anything.</summary>
    private DtoBaseConfiguration _liveConfiguration = new();

    /// <summary>Everything the assistant handed to the car saving service, in the order it did so.</summary>
    private readonly List<CarBasicConfiguration> _savedCarConfigurations = new();

    /// <summary>The order the application steps ran in, so a test can prove the base configuration went first.</summary>
    private readonly List<string> _operationOrder = new();

    public SetupApplicationServiceTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
        _configurationWrapper.Setup(w => w.GetBaseConfigurationAsync()).ReturnsAsync(() => _liveConfiguration);
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
            .Callback(() => _operationOrder.Add("chargePrice"))
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

    private SetupApplicationService NewService() => new(
        Moq.Mock.Of<ILogger<SetupApplicationService>>(),
        _setupStateService.Object,
        _baseConfigurationService.Object,
        _configurationWrapper.Object,
        _chargingCostService.Object,
        _configJsonService.Object,
        Context,
        new FakeDateTimeProvider(CurrentFakeDate.UtcDateTime));

    private static DtoSetupState StateWithOneCar(bool shouldBeActivated = true) => new()
    {
        Configuration = new DtoBaseConfiguration { HomeGeofenceLatitude = 48.13, HomeGeofenceRadius = 120, },
        ChargePrice = new DtoChargePrice { GridPrice = 0.31m, },
        CarDrafts =
        {
            new DtoSetupCarDraft
            {
                CarId = 5,
                ShouldBeActivated = shouldBeActivated,
                ConnectionRoute = SetupCarConnectionRoute.TeslaCloud,
                Configuration = new CarBasicConfiguration
                {
                    Id = 5, Name = "Car", Vin = "VIN1", UsableEnergy = 75, MaximumPhases = 3,
                    MinimumAmpere = 6, MaximumAmpere = 16, ChargingPriority = 1, ShouldBeManaged = true,
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
    public async Task SavingDuringSetupNeverHandsACarToTheChargingScheduler()
    {
        var state = StateWithOneCar();
        //Even a draft that already says "managed" is saved unmanaged: activation is its own explicit step.
        state.CarDrafts[0].Configuration.ShouldBeManaged = true;

        await NewService().ApplyConfiguration(state);

        Assert.False(Assert.Single(_savedCarConfigurations).ShouldBeManaged);
    }

    [Fact]
    public async Task ApplyingDoesNotEditTheDraftTheUserIsStillWorkingOn()
    {
        var state = StateWithOneCar();

        await NewService().ApplyConfiguration(state);

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
        //Saved once unmanaged while applying, then again managed while activating.
        Assert.Equal(2, _savedCarConfigurations.Count);
        Assert.False(_savedCarConfigurations[0].ShouldBeManaged);
        Assert.True(_savedCarConfigurations[1].ShouldBeManaged);
        _setupStateService.Verify(s => s.DeleteSetupState(), Times.Once);
    }

    [Fact]
    public async Task ACarTheUserDoesNotWantEnabledIsSavedButNotActivated()
    {
        var result = await NewService().ActivateAndCompleteSetup(StateWithOneCar(shouldBeActivated: false));

        Assert.True(result.IsSetupCompleted);
        Assert.False(Assert.Single(_savedCarConfigurations).ShouldBeManaged);
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
            .Callback(() => _operationOrder.Add("chargePrice"))
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
        state.ChargerDrafts.Add(new DtoSetupChargerDraft { ConnectorId = 7, ShouldBeActivated = true, AllowGuestCars = true, });

        var result = await NewService().ActivateAndCompleteSetup(state);

        Assert.True(result.IsSetupCompleted);
        var connector = await Context.OcppChargingStationConnectors.FirstAsync(c => c.Id == 7);
        Assert.True(connector.ShouldBeManaged);
        Assert.True(connector.AllowGuestCars);
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
}
