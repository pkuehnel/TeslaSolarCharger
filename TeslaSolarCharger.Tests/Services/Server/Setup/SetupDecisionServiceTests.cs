using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Dtos.Setup;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Localization;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server.Setup;

public class SetupDecisionServiceTests
{
    private static SetupDecisionService NewService(DtoSetupCapabilities capabilities)
    {
        var probe = new Mock<ISetupCapabilityProbe>();
        probe.Setup(p => p.GetCapabilities()).ReturnsAsync(capabilities);
        return new SetupDecisionService(Mock.Of<ILogger<SetupDecisionService>>(), probe.Object);
    }

    /// <summary>Everything detected and connected, so a test only has to describe what it wants to be different.</summary>
    private static DtoSetupCapabilities FullyCapable() => new()
    {
        BackendTokenState = TokenState.UpToDate,
        IsBaseAppLicensed = true,
        FleetApiTokenState = TokenState.UpToDate,
        HasGridPowerSource = true,
        HasSolarGenerationSource = true,
        HasHomeBatterySocSource = true,
        HasHomeBatteryPowerSource = true,
    };

    /// <summary>A state whose every step is complete, so a test can make exactly one thing incomplete.</summary>
    private static DtoSetupState CompleteState() => new()
    {
        CurrentStep = SetupStepKey.Finish,
        CompletedSteps = new List<SetupStepKey> { SetupStepKey.Welcome, },
        HasPvSystem = true,
        HasHomeBattery = false,
        Configuration = new DtoBaseConfiguration { HomeGeofenceLatitude = 48.13, HomeGeofenceLongitude = 11.58, },
        ChargePrice = new DtoChargePrice { GridPrice = 0.31m, },
        CarDrafts = { ValidCarDraft(), },
    };

    private static DtoSetupCarDraft ValidCarDraft(SetupCarConnectionRoute route = SetupCarConnectionRoute.TeslaCloud) => new()
    {
        CarId = 7,
        ConnectionRoute = route,
        Configuration = new CarBasicConfiguration
        {
            Id = 7,
            Name = "Car",
            Vin = "VIN123",
            UsableEnergy = 75,
            MaximumPhases = 3,
            MinimumAmpere = 6,
            MaximumAmpere = 16,
            ChargingPriority = 1,
            CarType = CarType.Tesla,
            UseFleetTelemetry = route == SetupCarConnectionRoute.TeslaCloud,
            UseBle = route == SetupCarConnectionRoute.TeslaBluetooth,
            BleApiBaseUrl = route == SetupCarConnectionRoute.TeslaBluetooth ? "http://ble" : null,
            HomeDetectionVia = route == SetupCarConnectionRoute.TeslaBluetooth ? HomeDetectionVia.BlePresence : HomeDetectionVia.GpsLocation,
        },
    };

    private static DtoSetupStepStatus Step(DtoSetupDecision decision, SetupStepKey key) =>
        decision.Steps.Single(s => s.StepKey == key);

    [Fact]
    public async Task CompleteState_IsReportedComplete()
    {
        var decision = await NewService(FullyCapable()).Evaluate(CompleteState());

        Assert.True(decision.IsConfigurationComplete);
        Assert.Empty(decision.MissingInformation);
        Assert.Empty(decision.Incompatibilities);
    }

    [Fact]
    public async Task EveryStepIsReported_SoAScreenCanRenderTheWholeRoute()
    {
        var decision = await NewService(FullyCapable()).Evaluate(CompleteState());

        Assert.Equal(
            new[]
            {
                SetupStepKey.Welcome, SetupStepKey.CloudConnection, SetupStepKey.SolarAndBattery, SetupStepKey.Location,
                SetupStepKey.Prices, SetupStepKey.CarsAndCharging, SetupStepKey.Finish,
            },
            decision.Steps.Select(s => s.StepKey));
    }

    [Fact]
    public async Task NotSignedIn_AsksForTheAccountAndNotForTheLicence()
    {
        var capabilities = FullyCapable();
        capabilities.BackendTokenState = TokenState.NotAvailable;
        capabilities.IsBaseAppLicensed = false;

        var decision = await NewService(capabilities).Evaluate(CompleteState());

        var issue = Assert.Single(Step(decision, SetupStepKey.CloudConnection).Issues);
        Assert.Equal(TranslationKeys.SetupIssueCloudConnectionMissing, issue.MessageKey);
    }

    [Fact]
    public async Task LicenceLookupFailed_DoesNotClaimTheLicenceIsMissing()
    {
        var capabilities = FullyCapable();
        //Null means the check could not be carried out, which must not be reported as "you have no licence".
        capabilities.IsBaseAppLicensed = null;

        var decision = await NewService(capabilities).Evaluate(CompleteState());

        Assert.Empty(Step(decision, SetupStepKey.CloudConnection).Issues);
    }

    [Fact]
    public async Task LicenceExplicitlyMissing_IsReported()
    {
        var capabilities = FullyCapable();
        capabilities.IsBaseAppLicensed = false;

        var decision = await NewService(capabilities).Evaluate(CompleteState());

        var issue = Assert.Single(Step(decision, SetupStepKey.CloudConnection).Issues);
        Assert.Equal(TranslationKeys.SetupIssueBaseAppLicenseMissing, issue.MessageKey);
    }

    [Fact]
    public async Task UnansweredSolarQuestion_IsNotTreatedAsNo()
    {
        var state = CompleteState();
        state.HasPvSystem = null;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        Assert.Contains(Step(decision, SetupStepKey.SolarAndBattery).Issues,
            i => i.MessageKey == TranslationKeys.SetupIssuePvQuestionUnanswered);
        Assert.False(decision.IsConfigurationComplete);
    }

    [Fact]
    public async Task UnansweredHomeBatteryQuestion_IsAskedEvenWithoutSolarPanels()
    {
        var state = CompleteState();
        state.HasPvSystem = false;
        state.HasHomeBattery = null;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        Assert.Contains(Step(decision, SetupStepKey.SolarAndBattery).Issues,
            i => i.MessageKey == TranslationKeys.SetupIssueHomeBatteryQuestionUnanswered);
    }

    [Fact]
    public async Task SolarPanelsWithoutAGridMeasurement_AsksForTheDataSource()
    {
        var capabilities = FullyCapable();
        capabilities.HasGridPowerSource = false;

        var decision = await NewService(capabilities).Evaluate(CompleteState());

        Assert.Contains(Step(decision, SetupStepKey.SolarAndBattery).Issues,
            i => i.MessageKey == TranslationKeys.SetupIssueGridPowerSourceMissing);
    }

    [Fact]
    public async Task HomeBatteryWithoutSpecifications_AsksForCapacityAndChargingPower()
    {
        var state = CompleteState();
        state.HasHomeBattery = true;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        var issues = Step(decision, SetupStepKey.SolarAndBattery).Issues.Select(i => i.MessageKey).ToList();
        Assert.Contains(TranslationKeys.SetupIssueHomeBatteryCapacityUnknown, issues);
        Assert.Contains(TranslationKeys.SetupIssueHomeBatteryChargingPowerUnknown, issues);
    }

    [Fact]
    public async Task PlaceholderCoordinates_AreNotAcceptedAsAConfirmedAddress()
    {
        var state = CompleteState();
        //The coordinates a fresh configuration starts with.
        state.Configuration.HomeGeofenceLatitude = new DtoBaseConfiguration().HomeGeofenceLatitude;
        state.Configuration.HomeGeofenceLongitude = new DtoBaseConfiguration().HomeGeofenceLongitude;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        Assert.Contains(Step(decision, SetupStepKey.Location).Issues,
            i => i.MessageKey == TranslationKeys.SetupIssueHomeLocationNotConfirmed);
    }

    [Fact]
    public async Task PlaceholderCoordinatesTheUserConfirmed_AreAccepted()
    {
        var state = CompleteState();
        state.Configuration.HomeGeofenceLatitude = new DtoBaseConfiguration().HomeGeofenceLatitude;
        state.Configuration.HomeGeofenceLongitude = new DtoBaseConfiguration().HomeGeofenceLongitude;
        state.ValueSources[nameof(BaseConfigurationBase.HomeGeofenceLatitude)] = SetupValueSource.UserEntered;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        Assert.Empty(Step(decision, SetupStepKey.Location).Issues);
    }

    [Fact]
    public async Task MissingGridPrice_IsReported()
    {
        var state = CompleteState();
        state.ChargePrice = null;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        Assert.Contains(Step(decision, SetupStepKey.Prices).Issues,
            i => i.MessageKey == TranslationKeys.SetupIssueGridPriceMissing);
    }

    [Fact]
    public async Task NoEquipmentAtAll_IsReportedAsSomethingToAdd()
    {
        var state = CompleteState();
        state.CarDrafts.Clear();

        var decision = await NewService(FullyCapable()).Evaluate(state);

        Assert.Contains(Step(decision, SetupStepKey.CarsAndCharging).Issues,
            i => i.MessageKey == TranslationKeys.SetupIssueNoEquipmentConfigured);
    }

    [Fact]
    public async Task ChargingStationWithoutACar_IsASupportedSetup()
    {
        var state = CompleteState();
        state.CarDrafts.Clear();
        state.ChargerDrafts.Add(new DtoSetupChargerDraft { ConnectorId = 3, ChargepointId = "CP1", });
        var capabilities = FullyCapable();
        capabilities.KnownChargingStationConnectorIds = new List<int> { 3, };

        var decision = await NewService(capabilities).Evaluate(state);

        Assert.True(decision.IsConfigurationComplete);
    }

    [Fact]
    public async Task ChargingStationThatHasNotReportedIn_IsReportedAndBlocksActivation()
    {
        var state = CompleteState();
        state.ChargerDrafts.Add(new DtoSetupChargerDraft { ConnectorId = 3, ChargepointId = "CP1", });

        var decision = await NewService(FullyCapable()).Evaluate(state);

        var connectorStatus = decision.DeviceStatuses.Single(d => d.DeviceKind == SetupDeviceKind.ChargingStationConnector);
        Assert.Equal(SetupActivationStatus.Blocked, connectorStatus.ActivationStatus);
        Assert.Contains(connectorStatus.ActivationBlockers, b => b.MessageKey == TranslationKeys.SetupIssueChargingStationNotConnected);
    }

    [Fact]
    public async Task CarWithoutAChosenRoute_IsOnlyAskedForTheRoute()
    {
        var state = CompleteState();
        state.CarDrafts[0].ConnectionRoute = SetupCarConnectionRoute.Undecided;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        var carStatus = decision.DeviceStatuses.Single(d => d.DeviceKind == SetupDeviceKind.Car);
        var blocker = Assert.Single(carStatus.ActivationBlockers);
        Assert.Equal(TranslationKeys.SetupIssueCarConnectionRouteUndecided, blocker.MessageKey);
    }

    [Fact]
    public async Task CarWithoutSpecifications_ReportsEachMissingFactSeparately()
    {
        var state = CompleteState();
        state.CarDrafts[0].Configuration.Name = null;
        state.CarDrafts[0].Configuration.Vin = string.Empty;
        state.CarDrafts[0].Configuration.UsableEnergy = 0;
        state.CarDrafts[0].Configuration.MaximumPhases = 0;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        var blockers = decision.DeviceStatuses.Single(d => d.DeviceKind == SetupDeviceKind.Car)
            .ActivationBlockers.Select(b => b.MessageKey).ToList();
        Assert.Contains(TranslationKeys.SetupIssueCarNameMissing, blockers);
        Assert.Contains(TranslationKeys.SetupIssueCarVinMissing, blockers);
        Assert.Contains(TranslationKeys.SetupIssueCarUsableEnergyUnknown, blockers);
        Assert.Contains(TranslationKeys.SetupIssueCarMaximumPhasesUnknown, blockers);
    }

    [Fact]
    public async Task BluetoothCarWithoutAnAddressForTheDevice_IsReported()
    {
        var state = CompleteState();
        state.CarDrafts[0] = ValidCarDraft(SetupCarConnectionRoute.TeslaBluetooth);
        state.CarDrafts[0].Configuration.BleApiBaseUrl = null;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        Assert.Contains(decision.MissingInformation, i => i.MessageKey == TranslationKeys.SetupIssueCarBleApiUrlMissing);
    }

    [Fact]
    public async Task CloudCarWithoutAConnectedTeslaAccount_IsReported()
    {
        var capabilities = FullyCapable();
        capabilities.FleetApiTokenState = TokenState.NotAvailable;

        var decision = await NewService(capabilities).Evaluate(CompleteState());

        Assert.Contains(decision.MissingInformation, i => i.MessageKey == TranslationKeys.SetupIssueTeslaAccountNotConnected);
    }

    [Fact]
    public async Task TeslaMateAndFleetTelemetryTogether_AreReportedAsContradictingEachOther()
    {
        var capabilities = FullyCapable();
        capabilities.UsesTeslaMateAsDataSource = true;

        var decision = await NewService(capabilities).Evaluate(CompleteState());

        var incompatibility = Assert.Single(decision.Incompatibilities);
        Assert.Equal(TranslationKeys.SetupIssueTeslaMateConflictsWithFleetTelemetry, incompatibility.MessageKey);
        Assert.Equal(SetupIssueSeverity.Incompatible, incompatibility.Severity);
    }

    [Fact]
    public async Task ANonTeslaWithNowhereToPlugInCannotCharge()
    {
        var state = CompleteState();
        state.CarDrafts[0] = new DtoSetupCarDraft
        {
            CarId = 8,
            ConnectionRoute = SetupCarConnectionRoute.ChargingStationOnly,
            Configuration = new CarBasicConfiguration
            {
                Id = 8, Name = "Hyundai", Vin = "VIN8", UsableEnergy = 64, MaximumPhases = 3,
                MinimumAmpere = 6, MaximumAmpere = 16, ChargingPriority = 1, CarType = CarType.Manual,
            },
        };

        var decision = await NewService(FullyCapable()).Evaluate(state);

        //However complete the rest looks, nothing could start or stop this car's charging.
        Assert.Contains(decision.MissingInformation, i => i.MessageKey == TranslationKeys.SetupIssueCarNeedsChargingStation);
    }

    [Fact]
    public async Task ANonTeslaWithAChargingStationIsFine()
    {
        var state = CompleteState();
        state.CarDrafts[0] = new DtoSetupCarDraft
        {
            CarId = 8,
            ConnectionRoute = SetupCarConnectionRoute.ChargingStationOnly,
            AssignedChargingConnectorIds = { 3, },
            Configuration = new CarBasicConfiguration
            {
                Id = 8, Name = "Hyundai", Vin = "VIN8", UsableEnergy = 64, MaximumPhases = 3,
                MinimumAmpere = 6, MaximumAmpere = 16, ChargingPriority = 1, CarType = CarType.Manual,
            },
        };
        var capabilities = FullyCapable();
        capabilities.KnownChargingStationConnectorIds = new List<int> { 3, };

        var decision = await NewService(capabilities).Evaluate(state);

        Assert.True(decision.IsConfigurationComplete);
    }

    [Fact]
    public async Task ASmartCarRouteWithoutAConnectedAccountIsReported()
    {
        var state = CompleteState();
        state.CarDrafts[0] = new DtoSetupCarDraft
        {
            CarId = 8,
            ConnectionRoute = SetupCarConnectionRoute.SmartCarWithChargingStation,
            AssignedChargingConnectorIds = { 3, },
            Configuration = new CarBasicConfiguration
            {
                Id = 8, Name = "Hyundai", Vin = "VIN8", UsableEnergy = 64, MaximumPhases = 3,
                MinimumAmpere = 6, MaximumAmpere = 16, ChargingPriority = 1,
                //Still a manual car: the account was never connected.
                CarType = CarType.Manual,
            },
        };
        var capabilities = FullyCapable();
        capabilities.KnownChargingStationConnectorIds = new List<int> { 3, };

        var decision = await NewService(capabilities).Evaluate(state);

        Assert.Contains(decision.MissingInformation, i => i.MessageKey == TranslationKeys.SetupIssueCarSmartCarNotConnected);
    }

    [Fact]
    public async Task AlreadyManagedCar_IsReportedActiveRatherThanWaitingToBeSwitchedOn()
    {
        var state = CompleteState();
        state.CarDrafts[0].Configuration.ShouldBeManaged = true;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        Assert.Equal(SetupActivationStatus.Active, decision.DeviceStatuses.Single(d => d.DeviceKind == SetupDeviceKind.Car).ActivationStatus);
    }

    [Fact]
    public async Task ConfiguredCarTheUserDoesNotWantActivated_StaysADraft()
    {
        var state = CompleteState();
        state.CarDrafts[0].Configuration.ShouldBeManaged = false;
        state.CarDrafts[0].ShouldBeActivated = false;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        var carStatus = decision.DeviceStatuses.Single(d => d.DeviceKind == SetupDeviceKind.Car);
        Assert.Equal(SetupActivationStatus.Draft, carStatus.ActivationStatus);
        Assert.Equal(SetupCompletionStatus.Complete, carStatus.ConfigurationStatus);
    }

    [Fact]
    public async Task FullyConfiguredCarWaitingToBeSwitchedOn_IsReadyForActivation()
    {
        var state = CompleteState();
        state.CarDrafts[0].Configuration.ShouldBeManaged = false;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        Assert.Equal(SetupActivationStatus.ReadyForActivation,
            decision.DeviceStatuses.Single(d => d.DeviceKind == SetupDeviceKind.Car).ActivationStatus);
    }

    [Fact]
    public async Task HomeBattery_ProposesTheAutomaticReserveByDefault()
    {
        var state = CompleteState();
        state.HasHomeBattery = true;
        state.Configuration.HomeBatteryUsableEnergy = 10;
        state.Configuration.HomeBatteryChargingPower = 3000;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        var proposal = decision.ProposedValues.Single(p => p.PropertyName == nameof(BaseConfigurationBase.DynamicHomeBatteryMinSoc));
        Assert.Equal(true, proposal.Value);
        Assert.False(proposal.IsPending);
    }

    [Fact]
    public async Task AutomaticReserveWithoutBatteryFacts_StaysProposedButPending()
    {
        var state = CompleteState();
        state.HasHomeBattery = true;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        var proposal = decision.ProposedValues.Single(p => p.PropertyName == nameof(BaseConfigurationBase.DynamicHomeBatteryMinSoc));
        //The requested choice stays visible and is marked pending instead of silently turning into a manual value.
        Assert.Equal(true, proposal.Value);
        Assert.True(proposal.IsPending);
        Assert.Contains(proposal.PendingReasons, r => r.MessageKey == TranslationKeys.SetupIssueHomeBatteryCapacityUnknown);
    }

    [Fact]
    public async Task ExplicitlyChosenReserve_IsNotOverriddenByTheProposal()
    {
        var state = CompleteState();
        state.HasHomeBattery = true;
        state.Configuration.DynamicHomeBatteryMinSoc = false;
        state.ValueSources[nameof(BaseConfigurationBase.DynamicHomeBatteryMinSoc)] = SetupValueSource.UserEntered;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        Assert.DoesNotContain(decision.ProposedValues, p => p.PropertyName == nameof(BaseConfigurationBase.DynamicHomeBatteryMinSoc));
    }

    [Fact]
    public async Task NoHomeBattery_DoesNotProposeABatteryReserve()
    {
        var decision = await NewService(FullyCapable()).Evaluate(CompleteState());

        Assert.DoesNotContain(decision.ProposedValues, p => p.PropertyName == nameof(BaseConfigurationBase.DynamicHomeBatteryMinSoc));
    }

    [Fact]
    public async Task SolarPanels_ProposeForecastingAndUsingItForScheduling()
    {
        var decision = await NewService(FullyCapable()).Evaluate(CompleteState());

        var propertyNames = decision.ProposedValues.Select(p => p.PropertyName).ToList();
        Assert.Contains(nameof(BaseConfigurationBase.PredictSolarPowerGeneration), propertyNames);
        Assert.Contains(nameof(BaseConfigurationBase.UsePredictedSolarPowerGenerationForChargingSchedules), propertyNames);
    }

    [Fact]
    public async Task SolarForecastWithoutAConfirmedLocation_IsPending()
    {
        var state = CompleteState();
        state.Configuration.HomeGeofenceLatitude = new DtoBaseConfiguration().HomeGeofenceLatitude;
        state.Configuration.HomeGeofenceLongitude = new DtoBaseConfiguration().HomeGeofenceLongitude;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        var proposal = decision.ProposedValues.Single(p => p.PropertyName == nameof(BaseConfigurationBase.PredictSolarPowerGeneration));
        Assert.True(proposal.IsPending);
        Assert.Contains(proposal.PendingReasons, r => r.MessageKey == TranslationKeys.SetupIssueHomeLocationNotConfirmed);
    }

    [Fact]
    public async Task BluetoothCar_DecidesTheInstallationWideBluetoothSwitchForTheUser()
    {
        var state = CompleteState();
        state.CarDrafts[0] = ValidCarDraft(SetupCarConnectionRoute.TeslaBluetooth);

        var decision = await NewService(FullyCapable()).Evaluate(state);

        var proposal = decision.ProposedValues.Single(p => p.PropertyName == nameof(BaseConfigurationBase.GetVehicleDataViaBle));
        Assert.Equal(true, proposal.Value);
    }

    [Fact]
    public async Task NoBluetoothCar_LeavesTheInstallationWideBluetoothSwitchAlone()
    {
        var decision = await NewService(FullyCapable()).Evaluate(CompleteState());

        Assert.DoesNotContain(decision.ProposedValues, p => p.PropertyName == nameof(BaseConfigurationBase.GetVehicleDataViaBle));
    }

    [Fact]
    public async Task BluetoothCar_ProposesMatchingHomeDetection()
    {
        var state = CompleteState();
        state.CarDrafts[0] = ValidCarDraft(SetupCarConnectionRoute.TeslaBluetooth);
        state.CarDrafts[0].Configuration.HomeDetectionVia = HomeDetectionVia.GpsLocation;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        var proposal = decision.ProposedValues.Single(p => p.PropertyName == nameof(CarBasicConfiguration.HomeDetectionVia));
        Assert.Equal(HomeDetectionVia.BlePresence, proposal.Value);
        Assert.Equal(state.CarDrafts[0].DraftId, proposal.DraftId);
    }

    [Fact]
    public async Task CarWithoutAnOrder_GetsOneWithoutAskingTheUserForANumber()
    {
        var state = CompleteState();
        state.CarDrafts[0].Configuration.ChargingPriority = 0;
        state.CarDrafts.Add(ValidCarDraft());
        state.CarDrafts[1].Configuration.ChargingPriority = 0;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        var priorities = decision.ProposedValues
            .Where(p => p.PropertyName == nameof(CarBasicConfiguration.ChargingPriority))
            .Select(p => p.Value)
            .ToList();
        Assert.Equal(new object?[] { 1, 2, }, priorities);
    }

    [Fact]
    public async Task NextAction_PointsAtTheFirstThingThatIsActuallyMissing()
    {
        var state = CompleteState();
        state.ChargePrice = null;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        Assert.Equal(SetupStepKey.Prices, decision.NextAction!.StepKey);
        Assert.Equal(TranslationKeys.SetupNextActionEnterPrices, decision.NextAction.DescriptionKey);
    }

    [Fact]
    public async Task NextAction_NamesTheCarThatNeedsWork()
    {
        var state = CompleteState();
        state.CarDrafts[0].Configuration.UsableEnergy = 0;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        Assert.Equal(SetupStepKey.CarsAndCharging, decision.NextAction!.StepKey);
        Assert.Equal(state.CarDrafts[0].DraftId, decision.NextAction.DraftId);
    }

    [Fact]
    public async Task NothingLeftToDo_PointsAtFinishing()
    {
        var decision = await NewService(FullyCapable()).Evaluate(CompleteState());

        Assert.Equal(SetupStepKey.Finish, decision.NextAction!.StepKey);
    }

    [Fact]
    public async Task APostponedChargingTest_DoesNotMakeTheConfigurationIncomplete()
    {
        var state = CompleteState();
        state.CarDrafts[0].ConnectionCheckState = SetupCheckResultState.NotRun;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        Assert.True(decision.IsConfigurationComplete);
        Assert.Equal(SetupCheckResultState.NotRun,
            decision.DeviceStatuses.Single(d => d.DeviceKind == SetupDeviceKind.Car).ConnectionCheckState);
    }

    [Fact]
    public async Task ACloudTeslaIsGivenAHomeDetectionItsOwnValidatorAccepts()
    {
        //Fleet Telemetry without tracking relevant fields does not report a position, so comparing one to the home
        //location cannot work - and the car validator refuses exactly that combination.
        var state = CompleteState();
        state.CarDrafts[0].ConnectionRoute = SetupCarConnectionRoute.TeslaCloud;
        state.CarDrafts[0].Configuration.UseFleetTelemetry = true;
        state.CarDrafts[0].Configuration.IncludeTrackingRelevantFields = false;
        state.CarDrafts[0].Configuration.HomeDetectionVia = HomeDetectionVia.GpsLocation;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        var proposal = Assert.Single(decision.ProposedValues,
            p => p.PropertyName == nameof(CarBasicConfiguration.HomeDetectionVia));
        Assert.Equal(HomeDetectionVia.LocatedAtHome, proposal.Value);
        Assert.Equal(state.CarDrafts[0].DraftId, proposal.DraftId);
    }

    [Fact]
    public async Task ACarThatReportsItsPositionKeepsTheOrdinaryHomeDetection()
    {
        var state = CompleteState();
        state.CarDrafts[0].ConnectionRoute = SetupCarConnectionRoute.TeslaCloud;
        state.CarDrafts[0].Configuration.UseFleetTelemetry = true;
        state.CarDrafts[0].Configuration.IncludeTrackingRelevantFields = true;
        state.CarDrafts[0].Configuration.HomeDetectionVia = HomeDetectionVia.GpsLocation;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        Assert.DoesNotContain(decision.ProposedValues, p => p.PropertyName == nameof(CarBasicConfiguration.HomeDetectionVia));
    }

    [Fact]
    public async Task ASettingThisInstallationAlreadyDecidedIsNotProposedAgain()
    {
        //A forecast switched off on purpose reads exactly like one never switched on. Offering it back as a ticked
        //recommendation would undo a deliberate choice on the next finish.
        var state = CompleteState();
        state.Configuration.PredictSolarPowerGeneration = false;
        state.ValueSources[nameof(BaseConfigurationBase.PredictSolarPowerGeneration)] = SetupValueSource.ExistingConfiguration;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        Assert.DoesNotContain(decision.ProposedValues,
            p => p.PropertyName == nameof(BaseConfigurationBase.PredictSolarPowerGeneration));
    }

    [Fact]
    public async Task DecliningTheForecastDoesNotLeaveSchedulingOnIt()
    {
        var state = CompleteState();
        state.ValueSources[nameof(BaseConfigurationBase.UsePredictedSolarPowerGenerationForChargingSchedules)] =
            SetupValueSource.UserEntered;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        //Scheduling on the forecast is its own decision, not something bundled with the forecast itself.
        Assert.Contains(decision.ProposedValues, p => p.PropertyName == nameof(BaseConfigurationBase.PredictSolarPowerGeneration));
        Assert.DoesNotContain(decision.ProposedValues,
            p => p.PropertyName == nameof(BaseConfigurationBase.UsePredictedSolarPowerGenerationForChargingSchedules));
    }

    [Fact]
    public async Task ABatteryWithoutPanelsStillGetsTheForecastItsReserveNeeds()
    {
        //The base configuration validator refuses an automatic reserve without the solar forecast, so proposing one
        //without the other would produce a combination the app will not store.
        var state = CompleteState();
        state.HasPvSystem = false;
        state.HasHomeBattery = true;
        state.Configuration.HomeBatteryUsableEnergy = 10;
        state.Configuration.HomeBatteryChargingPower = 3000;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        Assert.Contains(decision.ProposedValues, p => p.PropertyName == nameof(BaseConfigurationBase.DynamicHomeBatteryMinSoc));
        Assert.Contains(decision.ProposedValues, p => p.PropertyName == nameof(BaseConfigurationBase.PredictSolarPowerGeneration));
    }

    [Fact]
    public async Task AReserveSwitchedOnByHandStillSaysWhatItIsMissing()
    {
        //Once the user sets it themselves there is no proposal left to carry the pending reasons, but the forecast
        //it depends on has not become any less necessary.
        var state = CompleteState();
        state.HasHomeBattery = true;
        state.Configuration.HomeBatteryUsableEnergy = 10;
        state.Configuration.HomeBatteryChargingPower = 3000;
        state.Configuration.DynamicHomeBatteryMinSoc = true;
        state.Configuration.PredictSolarPowerGeneration = false;
        state.ValueSources[nameof(BaseConfigurationBase.PredictSolarPowerGeneration)] = SetupValueSource.UserEntered;

        var decision = await NewService(FullyCapable()).Evaluate(state);

        Assert.DoesNotContain(decision.ProposedValues, p => p.PropertyName == nameof(BaseConfigurationBase.DynamicHomeBatteryMinSoc));
        Assert.Contains(decision.MissingInformation, i => i.MessageKey == TranslationKeys.SetupIssueSolarPredictionRequired);
        Assert.False(decision.IsConfigurationComplete);
    }

    [Fact]
    public async Task AMultiConnectorChargerIsBlockedUntilAConnectorIsChosen()
    {
        //It used to be skipped silently at activation while setup still reported itself finished, leaving the user
        //with a charger they believe is switched on.
        var state = CompleteState();
        state.ChargerDrafts.Add(new DtoSetupChargerDraft
        {
            ChargepointId = "CP1", ChargingStationId = 2, ConnectorId = null, ShouldBeActivated = true,
        });

        var decision = await NewService(FullyCapable()).Evaluate(state);

        Assert.False(decision.IsConfigurationComplete);
        var status = decision.DeviceStatuses.Single(d => d.DeviceKind == SetupDeviceKind.ChargingStationConnector);
        Assert.Equal(SetupActivationStatus.Blocked, status.ActivationStatus);
        Assert.Contains(status.ActivationBlockers, b => b.MessageKey == TranslationKeys.SetupIssueChargingStationConnectorNotChosen);
    }

    [Fact]
    public async Task ATariffNobodyHasEnteredIsStillMissing()
    {
        //The assistant no longer fills the field with an example, so an unanswered tariff arrives as null rather
        //than as a plausible looking number.
        var state = CompleteState();
        state.ChargePrice = new DtoChargePrice { GridPrice = null, };

        var decision = await NewService(FullyCapable()).Evaluate(state);

        Assert.False(decision.IsConfigurationComplete);
        Assert.Contains(decision.MissingInformation, i => i.MessageKey == TranslationKeys.SetupIssueGridPriceMissing);
    }
}
