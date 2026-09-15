using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Dtos.Setup;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Server.Services;

public class SetupDecisionService(
    ILogger<SetupDecisionService> logger,
    ISetupCapabilityProbe capabilityProbe)
    : ISetupDecisionService
{
    /// <summary>
    /// The coordinates a fresh configuration starts with. They are a placeholder in the middle of Berlin, not an
    /// address the user confirmed, so a configuration still sitting on them counts as unanswered.
    /// </summary>
    private static readonly DtoBaseConfiguration PlaceholderConfiguration = new();

    /// <summary>The order the user walks through the assistant. Also the order the next action is picked in.</summary>
    private static readonly SetupStepKey[] StepOrder =
    [
        SetupStepKey.Welcome,
        SetupStepKey.CloudConnection,
        SetupStepKey.SolarAndBattery,
        SetupStepKey.Location,
        SetupStepKey.Prices,
        SetupStepKey.CarsAndCharging,
        SetupStepKey.Finish,
    ];

    public async Task<DtoSetupDecision> Evaluate(DtoSetupState setupState)
    {
        logger.LogTrace("{method}(...)", nameof(Evaluate));
        var capabilities = await capabilityProbe.GetCapabilities().ConfigureAwait(false);
        var decision = new DtoSetupDecision();

        decision.DeviceStatuses.AddRange(BuildCarStatuses(setupState, capabilities));
        decision.DeviceStatuses.AddRange(BuildChargingStationStatuses(setupState, capabilities));

        foreach (var stepKey in StepOrder)
        {
            decision.Steps.Add(BuildStepStatus(stepKey, setupState, capabilities, decision.DeviceStatuses));
        }

        decision.ProposedValues.AddRange(BuildProposals(setupState, capabilities));

        var allIssues = decision.Steps.SelectMany(s => s.Issues).ToList();
        decision.MissingInformation.AddRange(allIssues.Where(i => i.Severity == SetupIssueSeverity.MissingInformation));
        decision.Incompatibilities.AddRange(allIssues.Where(i => i.Severity == SetupIssueSeverity.Incompatible));

        //Welcome and Finish carry no configuration: one is read and moved past, the other is the act of finishing
        //itself. Counting them would make an installation that is fully described look incomplete right up to the
        //moment it is finished.
        decision.IsConfigurationComplete = decision.Steps
            .Where(s => s.IsApplicable && IsConfigurationStep(s.StepKey))
            .All(s => s.Status is SetupCompletionStatus.Complete or SetupCompletionStatus.NotApplicable);

        decision.NextAction = BuildNextAction(decision);
        return decision;
    }

    private static bool IsConfigurationStep(SetupStepKey stepKey) =>
        stepKey is not (SetupStepKey.Welcome or SetupStepKey.Finish);

    private DtoSetupStepStatus BuildStepStatus(SetupStepKey stepKey,
        DtoSetupState state,
        DtoSetupCapabilities capabilities,
        List<DtoSetupDeviceStatus> deviceStatuses)
    {
        var status = new DtoSetupStepStatus { StepKey = stepKey, };
        switch (stepKey)
        {
            case SetupStepKey.Welcome:
                //Nothing to configure here; it is done as soon as the user has read it and moved on.
                status.Status = state.IsStepCompleted(SetupStepKey.Welcome) || state.CurrentStep != SetupStepKey.Welcome
                    ? SetupCompletionStatus.Complete
                    : SetupCompletionStatus.NotStarted;
                break;
            case SetupStepKey.CloudConnection:
                AddCloudConnectionIssues(status, capabilities);
                break;
            case SetupStepKey.SolarAndBattery:
                AddSolarAndBatteryIssues(status, state, capabilities);
                break;
            case SetupStepKey.Location:
                AddLocationIssues(status, state);
                break;
            case SetupStepKey.Prices:
                AddPriceIssues(status, state);
                break;
            case SetupStepKey.CarsAndCharging:
                AddEquipmentIssues(status, state, capabilities, deviceStatuses);
                break;
            case SetupStepKey.Finish:
                status.Status = state.CompletedOperations.Any(o => o.OperationKey == SetupOperationKey.CompleteSetup)
                    ? SetupCompletionStatus.Complete
                    : SetupCompletionStatus.NotStarted;
                break;
        }

        if (stepKey is SetupStepKey.Welcome or SetupStepKey.Finish)
        {
            return status;
        }

        status.Status = status.Issues.Count == 0
            ? SetupCompletionStatus.Complete
            : state.IsStepCompleted(stepKey) || state.CurrentStep == stepKey
                ? SetupCompletionStatus.InProgress
                : SetupCompletionStatus.NotStarted;
        return status;
    }

    private static void AddCloudConnectionIssues(DtoSetupStepStatus status, DtoSetupCapabilities capabilities)
    {
        if (capabilities.BackendTokenState != TokenState.UpToDate)
        {
            status.Issues.Add(Issue(TranslationKeys.SetupIssueCloudConnectionMissing, SetupStepKey.CloudConnection));
            return;
        }

        //A licence check that could not be carried out leaves IsBaseAppLicensed null. Telling the user their
        //licence is missing on the strength of a failed lookup would send them shopping for what they may own, so
        //only an explicit "not licensed" is reported.
        if (capabilities.IsBaseAppLicensed == false)
        {
            status.Issues.Add(Issue(TranslationKeys.SetupIssueBaseAppLicenseMissing, SetupStepKey.CloudConnection));
        }
    }

    private static void AddSolarAndBatteryIssues(DtoSetupStepStatus status, DtoSetupState state, DtoSetupCapabilities capabilities)
    {
        if (state.HasPvSystem == null)
        {
            status.Issues.Add(Issue(TranslationKeys.SetupIssuePvQuestionUnanswered, SetupStepKey.SolarAndBattery));
        }
        else if (state.HasPvSystem == true && !capabilities.HasGridPowerSource)
        {
            status.Issues.Add(Issue(TranslationKeys.SetupIssueGridPowerSourceMissing, SetupStepKey.SolarAndBattery));
        }

        if (state.HasHomeBattery == null)
        {
            status.Issues.Add(Issue(TranslationKeys.SetupIssueHomeBatteryQuestionUnanswered, SetupStepKey.SolarAndBattery));
            return;
        }

        if (state.HasHomeBattery != true)
        {
            return;
        }

        if (!capabilities.HasHomeBatterySocSource || !capabilities.HasHomeBatteryPowerSource)
        {
            status.Issues.Add(Issue(TranslationKeys.SetupIssueHomeBatterySourceMissing, SetupStepKey.SolarAndBattery));
        }

        foreach (var issue in GetHomeBatterySpecificationIssues(state))
        {
            status.Issues.Add(issue);
        }

        //Reported as a step issue and not only as a reason the proposal is pending, because once the user has
        //switched the automatic reserve on themselves there is no proposal left to carry it - and the combination
        //is one the base configuration validator refuses to store.
        if (state.Configuration.DynamicHomeBatteryMinSoc == true && !state.Configuration.PredictSolarPowerGeneration)
        {
            status.Issues.Add(Issue(TranslationKeys.SetupIssueSolarPredictionRequired, SetupStepKey.SolarAndBattery,
                propertyName: nameof(BaseConfigurationBase.PredictSolarPowerGeneration)));
        }
    }

    /// <summary>
    /// Everything the automatic reserve needs before it can work out a number: the battery's own facts, where the
    /// installation is, and a solar forecast to plan against. A forecast that is itself being proposed does not
    /// count as missing - accepting the recommendations turns it on in the same step.
    /// </summary>
    private static List<DtoSetupIssue> GetAutomaticReserveBlockers(DtoSetupState state,
        DtoBaseConfiguration configuration,
        List<DtoSetupProposedValue> proposals)
    {
        var blockers = GetHomeBatterySpecificationIssues(state);
        if (!IsHomeLocationConfirmed(state))
        {
            blockers.Add(Issue(TranslationKeys.SetupIssueHomeLocationNotConfirmed, SetupStepKey.Location));
        }

        if (!configuration.PredictSolarPowerGeneration
            && !proposals.Any(p => p.PropertyName == nameof(BaseConfigurationBase.PredictSolarPowerGeneration)))
        {
            blockers.Add(Issue(TranslationKeys.SetupIssueSolarPredictionRequired, SetupStepKey.SolarAndBattery,
                propertyName: nameof(BaseConfigurationBase.PredictSolarPowerGeneration)));
        }

        return blockers;
    }

    /// <summary>
    /// The battery facts the automatic reserve depends on. Reported both as step issues and as the reasons the
    /// automatic reserve is still pending, so the requested choice stays visible instead of turning back to manual.
    /// </summary>
    private static List<DtoSetupIssue> GetHomeBatterySpecificationIssues(DtoSetupState state)
    {
        var issues = new List<DtoSetupIssue>();
        if (state.Configuration.HomeBatteryUsableEnergy is not > 0)
        {
            issues.Add(Issue(TranslationKeys.SetupIssueHomeBatteryCapacityUnknown, SetupStepKey.SolarAndBattery,
                propertyName: nameof(BaseConfigurationBase.HomeBatteryUsableEnergy)));
        }

        if (state.Configuration.HomeBatteryChargingPower is not > 0)
        {
            issues.Add(Issue(TranslationKeys.SetupIssueHomeBatteryChargingPowerUnknown, SetupStepKey.SolarAndBattery,
                propertyName: nameof(BaseConfigurationBase.HomeBatteryChargingPower)));
        }

        return issues;
    }

    private static void AddLocationIssues(DtoSetupStepStatus status, DtoSetupState state)
    {
        if (!IsHomeLocationConfirmed(state))
        {
            status.Issues.Add(Issue(TranslationKeys.SetupIssueHomeLocationNotConfirmed, SetupStepKey.Location,
                propertyName: nameof(BaseConfigurationBase.HomeGeofenceLatitude)));
        }
    }

    /// <summary>
    /// A location counts as confirmed when the user picked it, or when it simply is not the placeholder any more.
    /// Both checks are needed: an installation configured before the assistant recorded provenance still has a real
    /// address, and a user may deliberately confirm a spot that happens to be the placeholder.
    /// </summary>
    private static bool IsHomeLocationConfirmed(DtoSetupState state)
    {
        if (state.ValueSources.TryGetValue(nameof(BaseConfigurationBase.HomeGeofenceLatitude), out var source)
            && source == SetupValueSource.UserEntered)
        {
            return true;
        }

        // ReSharper disable CompareOfFloatsByEqualityOperator
        return state.Configuration.HomeGeofenceLatitude != PlaceholderConfiguration.HomeGeofenceLatitude
               || state.Configuration.HomeGeofenceLongitude != PlaceholderConfiguration.HomeGeofenceLongitude;
        // ReSharper restore CompareOfFloatsByEqualityOperator
    }

    private static void AddPriceIssues(DtoSetupStepStatus status, DtoSetupState state)
    {
        //Null and zero mean the same thing here: nobody has said what electricity costs yet. The assistant starts
        //this field empty on purpose, so that an example number never passes for the user's own contract.
        if (state.ChargePrice?.GridPrice is not > 0)
        {
            status.Issues.Add(Issue(TranslationKeys.SetupIssueGridPriceMissing, SetupStepKey.Prices));
        }

        //A market tariff without a region cannot have prices fetched for it, and the app's own tariff validator
        //refuses to store one. Said here so the user answers it, rather than meeting it as a failed save at the end.
        if (state.ResolvedElectricityPriceKind == SetupElectricityPriceKind.Market
            && state.ChargePrice?.SpotPriceRegion == null)
        {
            status.Issues.Add(Issue(TranslationKeys.SetupIssueMarketRegionMissing, SetupStepKey.Prices,
                propertyName: nameof(DtoChargePrice.SpotPriceRegion)));
        }
    }

    private static void AddEquipmentIssues(DtoSetupStepStatus status,
        DtoSetupState state,
        DtoSetupCapabilities capabilities,
        List<DtoSetupDeviceStatus> deviceStatuses)
    {
        if (state.CarDrafts.Count == 0 && state.ChargerDrafts.Count == 0 && capabilities.KnownChargingStationConnectorIds.Count == 0)
        {
            status.Issues.Add(Issue(TranslationKeys.SetupIssueNoEquipmentConfigured, SetupStepKey.CarsAndCharging));
            return;
        }

        foreach (var deviceStatus in deviceStatuses)
        {
            status.Issues.AddRange(deviceStatus.ActivationBlockers);
        }
    }

    private static IEnumerable<DtoSetupDeviceStatus> BuildCarStatuses(DtoSetupState state, DtoSetupCapabilities capabilities)
    {
        foreach (var draft in state.CarDrafts)
        {
            var blockers = GetCarBlockers(draft, capabilities);
            yield return new DtoSetupDeviceStatus
            {
                DeviceKind = SetupDeviceKind.Car,
                DraftId = draft.DraftId,
                DeviceId = draft.CarId,
                DisplayName = draft.Configuration.Name ?? draft.Configuration.Vin,
                ConfigurationStatus = blockers.Count == 0 ? SetupCompletionStatus.Complete : SetupCompletionStatus.InProgress,
                ActivationStatus = GetActivationStatus(draft, blockers),
                ConnectionCheckState = draft.ConnectionCheckState,
                ActivationBlockers = blockers,
            };
        }
    }

    private static SetupActivationStatus GetActivationStatus(DtoSetupCarDraft draft, List<DtoSetupIssue> blockers)
    {
        //A car that was already managed keeps charging while the rest of the installation is being set up. Reporting
        //it as a draft would invite the user to "activate" equipment that is already running. Read from the flag
        //recorded when the draft was created: a new car's configuration says "should be managed" from the moment it
        //is constructed, so asking it would call every car the user adds an already running one.
        if (draft.WasManagedBeforeSetup && draft.ShouldBeActivated)
        {
            return SetupActivationStatus.Active;
        }

        if (!draft.ShouldBeActivated)
        {
            return SetupActivationStatus.Draft;
        }

        return blockers.Count == 0 ? SetupActivationStatus.ReadyForActivation : SetupActivationStatus.Blocked;
    }

    private static List<DtoSetupIssue> GetCarBlockers(DtoSetupCarDraft draft, DtoSetupCapabilities capabilities)
    {
        var blockers = new List<DtoSetupIssue>();
        var configuration = draft.Configuration;

        if (draft.ConnectionRoute == SetupCarConnectionRoute.Undecided)
        {
            blockers.Add(CarIssue(TranslationKeys.SetupIssueCarConnectionRouteUndecided, draft));
            //Without a route the remaining requirements are not known yet, so asking for them now would be guessing.
            return blockers;
        }

        if (string.IsNullOrWhiteSpace(configuration.Name))
        {
            blockers.Add(CarIssue(TranslationKeys.SetupIssueCarNameMissing, draft, nameof(CarBasicConfiguration.Name)));
        }

        if (string.IsNullOrWhiteSpace(configuration.Vin))
        {
            blockers.Add(CarIssue(TranslationKeys.SetupIssueCarVinMissing, draft, nameof(CarBasicConfiguration.Vin)));
        }

        //The validator treats anything up to 5 kWh as "not a car battery", so a zero here means unknown rather than
        //a car with no battery.
        if (configuration.UsableEnergy <= 5)
        {
            blockers.Add(CarIssue(TranslationKeys.SetupIssueCarUsableEnergyUnknown, draft, nameof(CarBasicConfiguration.UsableEnergy)));
        }

        if (configuration.MaximumPhases is < 1 or > 3)
        {
            blockers.Add(CarIssue(TranslationKeys.SetupIssueCarMaximumPhasesUnknown, draft, nameof(CarBasicConfiguration.MaximumPhases)));
        }

        if (configuration.MaximumAmpere < configuration.MinimumAmpere)
        {
            blockers.Add(CarIssue(TranslationKeys.SetupIssueCarCurrentLimitsInvalid, draft, nameof(CarBasicConfiguration.MaximumAmpere)));
        }

        switch (draft.ConnectionRoute)
        {
            case SetupCarConnectionRoute.TeslaBluetooth when string.IsNullOrWhiteSpace(configuration.BleApiBaseUrl):
                blockers.Add(CarIssue(TranslationKeys.SetupIssueCarBleApiUrlMissing, draft, nameof(CarBasicConfiguration.BleApiBaseUrl)));
                break;
            case SetupCarConnectionRoute.TeslaCloud when capabilities.FleetApiTokenState != TokenState.UpToDate:
                blockers.Add(CarIssue(TranslationKeys.SetupIssueTeslaAccountNotConnected, draft));
                break;
            case SetupCarConnectionRoute.SmartCarWithChargingStation when configuration.CarType != CarType.SmartCar:
                blockers.Add(CarIssue(TranslationKeys.SetupIssueCarSmartCarNotConnected, draft));
                break;
        }

        //A car that is not a Tesla is controlled by the charging station it is plugged into. Without one there is
        //nothing that could start or stop its charging, however complete the rest of its configuration looks.
        if (draft.ConnectionRoute is SetupCarConnectionRoute.SmartCarWithChargingStation or SetupCarConnectionRoute.ChargingStationOnly
            && draft.AssignedChargingConnectorIds.Count == 0
            && capabilities.KnownChargingStationConnectorIds.Count == 0)
        {
            blockers.Add(CarIssue(TranslationKeys.SetupIssueCarNeedsChargingStation, draft));
        }

        //TeslaMate and Fleet Telemetry are alternatives, not a combination. Saying so here keeps the user from
        //finding out through a server side validation failure at the end of setup.
        if (capabilities.UsesTeslaMateAsDataSource && configuration.UseFleetTelemetry)
        {
            blockers.Add(CarIssue(TranslationKeys.SetupIssueTeslaMateConflictsWithFleetTelemetry, draft,
                nameof(CarBasicConfiguration.UseFleetTelemetry), SetupIssueSeverity.Incompatible));
        }

        return blockers;
    }

    private static IEnumerable<DtoSetupDeviceStatus> BuildChargingStationStatuses(DtoSetupState state, DtoSetupCapabilities capabilities)
    {
        foreach (var draft in state.ChargerDrafts)
        {
            var isKnown = draft.ConnectorId != null && capabilities.KnownChargingStationConnectorIds.Contains(draft.ConnectorId.Value);
            var blockers = new List<DtoSetupIssue>();
            if (!isKnown)
            {
                blockers.Add(new DtoSetupIssue
                {
                    Severity = SetupIssueSeverity.MissingInformation,
                    //A station that has reported in but whose connector is still unchosen is a different problem
                    //from one that never connected, and it is the one a multi connector charger always hits.
                    MessageKey = draft.ChargingStationId != null
                        ? TranslationKeys.SetupIssueChargingStationConnectorNotChosen
                        : TranslationKeys.SetupIssueChargingStationNotConnected,
                    StepKey = SetupStepKey.CarsAndCharging,
                    DraftId = draft.DraftId,
                });
            }

            yield return new DtoSetupDeviceStatus
            {
                DeviceKind = SetupDeviceKind.ChargingStationConnector,
                DraftId = draft.DraftId,
                DeviceId = draft.ConnectorId,
                DisplayName = draft.ChargepointId,
                ConfigurationStatus = isKnown ? SetupCompletionStatus.Complete : SetupCompletionStatus.InProgress,
                ActivationStatus = !draft.ShouldBeActivated
                    ? SetupActivationStatus.Draft
                    : isKnown
                        ? SetupActivationStatus.ReadyForActivation
                        : SetupActivationStatus.Blocked,
                ConnectionCheckState = draft.ConnectionCheckState,
                ActivationBlockers = blockers,
            };
        }
    }

    private static List<DtoSetupProposedValue> BuildProposals(DtoSetupState state, DtoSetupCapabilities capabilities)
    {
        var proposals = new List<DtoSetupProposedValue>();
        var configuration = state.Configuration;

        //The automatic reserve is worked out from a solar forecast, which the base configuration validator insists
        //on. Proposing the reserve without it would produce a combination the app refuses to store, so the forecast
        //is proposed first and alongside - including for a battery in a household with no panels of its own.
        var isAutomaticReserveWanted = configuration.DynamicHomeBatteryMinSoc == true
                                       || (state.HasHomeBattery == true
                                           && IsUndecided(state, nameof(BaseConfigurationBase.DynamicHomeBatteryMinSoc), configuration.DynamicHomeBatteryMinSoc == null));

        if ((state.HasPvSystem == true || isAutomaticReserveWanted)
            && IsUndecided(state, nameof(BaseConfigurationBase.PredictSolarPowerGeneration), !configuration.PredictSolarPowerGeneration))
        {
            proposals.Add(new DtoSetupProposedValue
            {
                PropertyName = nameof(BaseConfigurationBase.PredictSolarPowerGeneration),
                Value = true,
                ReasonKey = state.HasPvSystem == true
                    ? TranslationKeys.SetupReasonPredictSolarPowerGeneration
                    : TranslationKeys.SetupReasonPredictSolarPowerGenerationForBattery,
                IsPending = !IsHomeLocationConfirmed(state),
                PendingReasons = IsHomeLocationConfirmed(state)
                    ? new List<DtoSetupIssue>()
                    : [Issue(TranslationKeys.SetupIssueHomeLocationNotConfirmed, SetupStepKey.Location)],
            });
        }

        //Enabled by default for a new installation with a battery: the reserve is what keeps the household supplied
        //in the evening, and working it out automatically is strictly better than asking a beginner for a number.
        if (state.HasHomeBattery == true
            && IsUndecided(state, nameof(BaseConfigurationBase.DynamicHomeBatteryMinSoc), configuration.DynamicHomeBatteryMinSoc == null))
        {
            var pendingReasons = GetAutomaticReserveBlockers(state, configuration, proposals);
            proposals.Add(new DtoSetupProposedValue
            {
                PropertyName = nameof(BaseConfigurationBase.DynamicHomeBatteryMinSoc),
                Value = true,
                ReasonKey = TranslationKeys.SetupReasonDynamicHomeBatteryMinSoc,
                IsPending = pendingReasons.Count > 0,
                PendingReasons = pendingReasons,
            });
        }

        //Scheduling on the forecast is its own decision. Bundling it with the forecast itself would re-enable
        //something an installation had turned off, just because it still wants the forecast.
        if (state.HasPvSystem == true
            && IsUndecided(state, nameof(BaseConfigurationBase.UsePredictedSolarPowerGenerationForChargingSchedules),
                !configuration.UsePredictedSolarPowerGenerationForChargingSchedules))
        {
            //Charging schedules may only use a forecast that is actually produced; the validator says so too.
            var pendingReasons = IsHomeLocationConfirmed(state)
                ? new List<DtoSetupIssue>()
                : [Issue(TranslationKeys.SetupIssueHomeLocationNotConfirmed, SetupStepKey.Location)];
            if (!configuration.PredictSolarPowerGeneration
                && !proposals.Any(p => p.PropertyName == nameof(BaseConfigurationBase.PredictSolarPowerGeneration)))
            {
                pendingReasons.Add(Issue(TranslationKeys.SetupIssueSolarPredictionRequired, SetupStepKey.SolarAndBattery,
                    propertyName: nameof(BaseConfigurationBase.PredictSolarPowerGeneration)));
            }

            proposals.Add(new DtoSetupProposedValue
            {
                PropertyName = nameof(BaseConfigurationBase.UsePredictedSolarPowerGenerationForChargingSchedules),
                Value = true,
                ReasonKey = TranslationKeys.SetupReasonUsePredictedSolarForSchedules,
                IsPending = pendingReasons.Count > 0,
                PendingReasons = pendingReasons,
            });
        }

        if (capabilities.HasGridPowerSource && IsUndecided(state, nameof(BaseConfigurationBase.ShowEnergyDataOnHome), !configuration.ShowEnergyDataOnHome))
        {
            proposals.Add(new DtoSetupProposedValue
            {
                PropertyName = nameof(BaseConfigurationBase.ShowEnergyDataOnHome),
                Value = true,
                ReasonKey = TranslationKeys.SetupReasonShowEnergyDataOnHome,
            });
        }

        //Reading battery levels over Bluetooth is an installation wide switch, but it is only ever needed because a
        //car was set up that way. Deciding it here is what keeps the user from being sent to the advanced settings
        //in the middle of setting up a car, and from hitting a save order failure when they are not.
        if (state.CarDrafts.Any(d => d.ConnectionRoute == SetupCarConnectionRoute.TeslaBluetooth)
            && IsUndecided(state, nameof(BaseConfigurationBase.GetVehicleDataViaBle), configuration.GetVehicleDataViaBle == null))
        {
            proposals.Add(new DtoSetupProposedValue
            {
                PropertyName = nameof(BaseConfigurationBase.GetVehicleDataViaBle),
                Value = true,
                ReasonKey = TranslationKeys.SetupReasonGetVehicleDataViaBle,
            });
        }

        proposals.AddRange(BuildCarProposals(state));
        return proposals;
    }

    private static IEnumerable<DtoSetupProposedValue> BuildCarProposals(DtoSetupState state)
    {
        for (var index = 0; index < state.CarDrafts.Count; index++)
        {
            var draft = state.CarDrafts[index];

            //A priority of zero is not a valid order, it is the value an untouched car starts with. Filling it in
            //order means a beginner never has to meet the concept at all.
            if (draft.Configuration.ChargingPriority <= 0)
            {
                yield return new DtoSetupProposedValue
                {
                    PropertyName = nameof(CarBasicConfiguration.ChargingPriority),
                    Value = index + 1,
                    ReasonKey = TranslationKeys.SetupReasonCarChargingPriority,
                    DraftId = draft.DraftId,
                };
            }

            //How the app can tell this car is at home follows from how it reaches the car. Asking the user to line
            //the two up themselves is exactly the knowledge they do not have, and getting it wrong is a
            //combination the car validator refuses - which used to surface only as a failure at the very end.
            var proposedHomeDetection = ProposeHomeDetection(draft);
            if (proposedHomeDetection == null || draft.Configuration.HomeDetectionVia == proposedHomeDetection)
            {
                continue;
            }

            yield return new DtoSetupProposedValue
            {
                PropertyName = nameof(CarBasicConfiguration.HomeDetectionVia),
                Value = proposedHomeDetection,
                ReasonKey = proposedHomeDetection == HomeDetectionVia.BlePresence
                    ? TranslationKeys.SetupReasonCarHomeDetectionViaBlePresence
                    : TranslationKeys.SetupReasonCarHomeDetectionViaLocatedAtHome,
                DraftId = draft.DraftId,
            };
        }
    }

    /// <summary>
    /// The home detection that goes with a car's connection route, or null where the default already fits. Tracking
    /// relevant fields change the answer for both Tesla routes: with them the car reports its position, without
    /// them home has to be decided from Bluetooth range or from what Tesla itself says about the car being home.
    /// </summary>
    private static HomeDetectionVia? ProposeHomeDetection(DtoSetupCarDraft draft)
    {
        if (draft.Configuration.IncludeTrackingRelevantFields)
        {
            //Position is being streamed, so the ordinary GPS comparison is the right one.
            return HomeDetectionVia.GpsLocation;
        }

        return draft.ConnectionRoute switch
        {
            SetupCarConnectionRoute.TeslaBluetooth => HomeDetectionVia.BlePresence,
            SetupCarConnectionRoute.TeslaCloud => HomeDetectionVia.LocatedAtHome,
            _ => null,
        };
    }

    /// <summary>
    /// Whether the app may decide a property. A value that already has the proposed effect is not proposed again,
    /// and neither is one somebody has already decided - by hand in the assistant, or by configuring this
    /// installation before the assistant existed. An installation that deliberately switched something off must not
    /// be offered a ticked box that switches it back on.
    /// </summary>
    private static bool IsUndecided(DtoSetupState state, string propertyName, bool differsFromProposal)
    {
        if (!differsFromProposal)
        {
            return false;
        }

        return !state.ValueSources.TryGetValue(propertyName, out var source)
               || source is not (SetupValueSource.UserEntered or SetupValueSource.ExistingConfiguration);
    }

    private static DtoSetupNextAction? BuildNextAction(DtoSetupDecision decision)
    {
        //Only steps that carry configuration can be "the next thing to do". Once they are all done the next thing
        //is finishing, which is an action in its own right rather than an unfinished step.
        var firstIncompleteStep = decision.Steps
            .FirstOrDefault(s => s.IsApplicable
                                 && IsConfigurationStep(s.StepKey)
                                 && s.Status is not (SetupCompletionStatus.Complete or SetupCompletionStatus.NotApplicable));
        if (firstIncompleteStep == null)
        {
            return new DtoSetupNextAction { StepKey = SetupStepKey.Finish, DescriptionKey = TranslationKeys.SetupNextActionFinish, };
        }

        //Point at the specific device that needs work rather than at the step in general, so returning from an
        //interruption lands on the exact task.
        var blockedDevice = decision.DeviceStatuses
            .FirstOrDefault(d => d.ActivationBlockers.Any(b => b.StepKey == firstIncompleteStep.StepKey));

        return new DtoSetupNextAction
        {
            StepKey = firstIncompleteStep.StepKey,
            DraftId = blockedDevice?.DraftId,
            DescriptionKey = GetNextActionKey(firstIncompleteStep.StepKey, blockedDevice != null),
        };
    }

    private static string GetNextActionKey(SetupStepKey stepKey, bool isAboutOneDevice) => stepKey switch
    {
        SetupStepKey.CloudConnection => TranslationKeys.SetupNextActionConnectCloud,
        SetupStepKey.SolarAndBattery => TranslationKeys.SetupNextActionDescribeSolarAndBattery,
        SetupStepKey.Location => TranslationKeys.SetupNextActionConfirmLocation,
        SetupStepKey.Prices => TranslationKeys.SetupNextActionEnterPrices,
        SetupStepKey.CarsAndCharging => isAboutOneDevice
            ? TranslationKeys.SetupNextActionCompleteCar
            : TranslationKeys.SetupNextActionAddEquipment,
        _ => TranslationKeys.SetupNextActionFinish,
    };

    private static DtoSetupIssue Issue(string messageKey,
        SetupStepKey stepKey,
        SetupIssueSeverity severity = SetupIssueSeverity.MissingInformation,
        string? propertyName = null) => new()
    {
        Severity = severity,
        MessageKey = messageKey,
        StepKey = stepKey,
        PropertyName = propertyName,
    };

    private static DtoSetupIssue CarIssue(string messageKey,
        DtoSetupCarDraft draft,
        string? propertyName = null,
        SetupIssueSeverity severity = SetupIssueSeverity.MissingInformation) => new()
    {
        Severity = severity,
        MessageKey = messageKey,
        StepKey = SetupStepKey.CarsAndCharging,
        DraftId = draft.DraftId,
        PropertyName = propertyName,
    };
}
