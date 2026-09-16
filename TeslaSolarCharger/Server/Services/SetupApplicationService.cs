using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Dtos.Setup;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class SetupApplicationService(
    ILogger<SetupApplicationService> logger,
    ISetupStateService setupStateService,
    IBaseConfigurationService baseConfigurationService,
    IConfigurationWrapper configurationWrapper,
    IChargingCostService chargingCostService,
    IConfigJsonService configJsonService,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    IDateTimeProvider dateTimeProvider,
    ISetupDecisionService setupDecisionService,
    IValidator<CarBasicConfiguration> carConfigurationValidator,
    IValidator<DtoBaseConfiguration> baseConfigurationValidator,
    IValidator<DtoChargePrice> chargePriceValidator)
    : ISetupApplicationService
{
    internal const int DefaultChargingStationMinCurrent = 6;

    public async Task<DtoSetupApplicationResult> ApplyConfiguration(DtoSetupState setupState)
    {
        logger.LogTrace("{method}(...)", nameof(ApplyConfiguration));
        var result = new DtoSetupApplicationResult();
        await ApplyConfigurationInternal(setupState, result).ConfigureAwait(false);
        return result;
    }

    public async Task<DtoSetupApplicationResult> ActivateAndCompleteSetup(DtoSetupState setupState)
    {
        logger.LogTrace("{method}(...)", nameof(ActivateAndCompleteSetup));
        var result = new DtoSetupApplicationResult();

        //The readiness checks are what the assistant shows the user; finishing has to be held to the same answer.
        //Checking it here rather than only in the browser is the difference between a rule and a suggestion: a
        //stale tab, a retried request or anything else posting this state must not be able to switch equipment on
        //that the app itself reports as not ready.
        var decision = await setupDecisionService.Evaluate(setupState).ConfigureAwait(false);
        if (!decision.CanFinishSetup)
        {
            result.Operations.Add(new DtoSetupOperationResult
            {
                OperationKey = SetupOperationKey.CompleteSetup,
                IsSuccess = false,
                ErrorMessage = "Setup is not ready to be finished yet: "
                               + string.Join(", ", decision.MissingInformation.Concat(decision.Incompatibilities)
                                   .Where(i => i.StepKey != SetupStepKey.CarsAndCharging)
                                   .Select(i => i.MessageKey).Distinct()),
                //Something to answer, not something to try again unchanged.
                IsRetryable = false,
            });
            return result;
        }

        await ApplyConfigurationInternal(setupState, result).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            //Activating equipment whose configuration did not save would switch on something other than what the
            //user reviewed, so stop here and keep the state for a retry.
            return result;
        }

        //Every car in setup that is ready is switched on. One that still misses something stays as it is: the finish
        //screen lists it as not controlled yet, and it can be set up later. A car the user does not want controlled
        //is taken out of setup instead, which leaves it exactly as it was too.
        var carDraftsToActivate = setupState.CarDrafts.Where(d => IsReadyToSwitchOn(decision, d.DraftId)).ToList();
        foreach (var draft in carDraftsToActivate)
        {
            await RunOperation(setupState, result, SetupOperationKey.ActivateCar, draft.DraftId,
                () => ActivateCar(draft), CarContent(draft)).ConfigureAwait(false);
        }

        //Filtered by what the readiness checks report, not by "has a connector": a charger reported ready that turns
        //out to have none is still a failure rather than something to skip quietly.
        var chargerDraftsToActivate = setupState.ChargerDrafts.Where(d => IsReadyToSwitchOn(decision, d.DraftId)).ToList();
        foreach (var draft in chargerDraftsToActivate)
        {
            await RunOperation(setupState, result, SetupOperationKey.ActivateChargingStationConnector, draft.DraftId,
                () => ActivateChargingStationConnector(draft),
                new { draft.ConnectorId, draft.AllowGuestCars, }).ConfigureAwait(false);
        }

        if (!result.IsSuccess)
        {
            return result;
        }

        await RunOperation(setupState, result, SetupOperationKey.CompleteSetup, null,
            CompleteSetup).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return result;
        }

        //Only now is it true that setup finished. Clearing the state any earlier would lose the answers that a
        //failed save still needs.
        result.IsSetupCompleted = true;
        await setupStateService.DeleteSetupState().ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Whether the readiness checks report this device as having nothing left to answer. A device they say nothing
    /// about is not switched on: finishing only ever starts what the app itself calls ready.
    /// </summary>
    private static bool IsReadyToSwitchOn(DtoSetupDecision decision, Guid draftId) =>
        decision.DeviceStatuses.FirstOrDefault(s => s.DraftId == draftId) is { ActivationBlockers.Count: 0, };

    public async Task<DtoSetupApplicationResult> SaveCarDraft(DtoSetupState setupState, Guid draftId)
    {
        logger.LogTrace("{method}(..., {draftId})", nameof(SaveCarDraft), draftId);
        var result = new DtoSetupApplicationResult();
        var draft = setupState.FindCarDraft(draftId);
        if (draft == null)
        {
            result.Operations.Add(new DtoSetupOperationResult
            {
                OperationKey = SetupOperationKey.SaveCarDraft,
                DraftId = draftId,
                IsSuccess = false,
                ErrorMessage = "The car is no longer part of this setup.",
            });
            return result;
        }

        //An explicit save means the user pressed something, so a record of an earlier save must not make this one a
        //no-op even when the values happen to be unchanged. Idempotency protects a retried finish, not a save the
        //user asked for.
        setupState.CompletedOperations.RemoveAll(o => o.OperationKey == SetupOperationKey.SaveCarDraft && o.DraftId == draftId);
        await RunOperation(setupState, result, SetupOperationKey.SaveCarDraft, draftId,
            () => SaveCarDraft(draft), CarContent(draft)).ConfigureAwait(false);
        return result;
    }

    public DtoSetupState AcceptProposals(DtoSetupState setupState, IReadOnlyCollection<DtoSetupProposedValue> proposals)
    {
        logger.LogTrace("{method}(..., {count} proposals)", nameof(AcceptProposals), proposals.Count);
        foreach (var proposal in proposals)
        {
            if (proposal.DraftId == null)
            {
                if (TrySetProperty(setupState.Configuration, proposal.PropertyName, proposal.Value))
                {
                    setupState.ValueSources[proposal.PropertyName] = proposal.Source;
                }

                continue;
            }

            var draft = setupState.FindCarDraft(proposal.DraftId.Value);
            if (draft == null)
            {
                logger.LogWarning("Proposal for unknown car draft {draftId} ignored.", proposal.DraftId);
                continue;
            }

            if (TrySetProperty(draft.Configuration, proposal.PropertyName, proposal.Value))
            {
                draft.ValueSources[proposal.PropertyName] = proposal.Source;
            }
        }

        return setupState;
    }

    private async Task ApplyConfigurationInternal(DtoSetupState setupState, DtoSetupApplicationResult result)
    {
        //The base configuration goes first on purpose. It carries the installation wide switches a car's own
        //validation reads - reading battery levels over Bluetooth above all - so saving a car before them would
        //fail on a setting the user already answered inside the assistant.
        await RunOperation(setupState, result, SetupOperationKey.SaveBaseConfiguration, null,
            () => SaveBaseConfiguration(setupState),
            SetupConfigurationOwnership.OwnedValues(setupState.Configuration)).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return;
        }

        await RunOperation(setupState, result, SetupOperationKey.SaveChargePrice, null,
            () => SaveChargePrice(setupState),
            new { setupState.ChargePrice, setupState.FixedPrices, setupState.ElectricityPriceKind, }).ConfigureAwait(false);

        //A car added but never identified has nothing to be written as. Now that finishing no longer waits for every
        //car, saving it anyway would leave a nameless car behind.
        foreach (var draft in setupState.CarDrafts.Where(d => d.CanBeStored()))
        {
            await RunOperation(setupState, result, SetupOperationKey.SaveCarDraft, draft.DraftId,
                () => SaveCarDraft(draft), CarContent(draft)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// What a car write actually depends on. Used to tell a retry of the same answers apart from a repeat of an
    /// operation whose answers have changed since.
    /// </summary>
    private static object CarContent(DtoSetupCarDraft draft) => new
    {
        draft.CarId,
        draft.Configuration,
        draft.WasManagedBeforeSetup,
        Assignments = draft.AssignedChargingConnectorIds.OrderBy(id => id).ToList(),
    };

    /// <summary>
    /// A stable fingerprint of what an operation is about to write. Null content means the operation has no inputs
    /// of its own - finishing setup, for one - and is then identified by the operation key alone.
    /// </summary>
    private static string ComputeContentHash(object? content)
    {
        var json = JsonConvert.SerializeObject(content ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    /// <summary>
    /// Runs one application step unless a previous attempt already wrote exactly this, records the outcome on the
    /// state and reports it. Recording as we go is what makes a retry after a partial failure safe; comparing what
    /// was written is what keeps a retry from swallowing an edit the user made in between.
    /// </summary>
    private async Task RunOperation(DtoSetupState setupState,
        DtoSetupApplicationResult result,
        SetupOperationKey operationKey,
        Guid? draftId,
        Func<Task> operation,
        object? content = null)
    {
        var contentHash = ComputeContentHash(content);
        var previousRecord = setupState.CompletedOperations
            .FirstOrDefault(o => o.OperationKey == operationKey && o.DraftId == draftId);
        //A record without a hash was written before the values were tracked, so it says nothing about whether the
        //answers still match. Running the step again is the safe reading: these writes are all idempotent.
        if (previousRecord != null && previousRecord.ContentHash != null && previousRecord.ContentHash == contentHash)
        {
            result.Operations.Add(new DtoSetupOperationResult
            {
                OperationKey = operationKey,
                DraftId = draftId,
                IsSuccess = true,
                WasAlreadyCompleted = true,
            });
            return;
        }

        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Setup operation {operationKey} for draft {draftId} failed.", operationKey, draftId);
            result.Operations.Add(new DtoSetupOperationResult
            {
                OperationKey = operationKey,
                DraftId = draftId,
                IsSuccess = false,
                ErrorMessage = exception.Message,
                //Validation tells the user to change something; anything else is worth trying again as it stands.
                IsRetryable = exception is not FluentValidation.ValidationException,
            });
            return;
        }

        setupState.CompletedOperations.RemoveAll(o => o.OperationKey == operationKey && o.DraftId == draftId);
        setupState.CompletedOperations.Add(new DtoSetupOperationRecord
        {
            OperationKey = operationKey,
            DraftId = draftId,
            ContentHash = contentHash,
            CompletedAt = dateTimeProvider.DateTimeOffSetUtcNow(),
        });
        await setupStateService.UpdateSetupState(setupState).ConfigureAwait(false);
        result.Operations.Add(new DtoSetupOperationResult
        {
            OperationKey = operationKey,
            DraftId = draftId,
            IsSuccess = true,
        });
    }

    private async Task SaveBaseConfiguration(DtoSetupState setupState)
    {
        //Start from what is configured right now and copy only the properties the assistant is responsible for.
        //Writing back the whole snapshot the assistant started with would undo anything changed in Base
        //Configuration while setup was open.
        var liveConfiguration = await configurationWrapper.GetBaseConfigurationAsync().ConfigureAwait(false);
        //IsFirstRun is deliberately not one of the owned properties: finishing is its own explicit step, and
        //clearing the flag here would make an installation look set up while its cars are still being saved.
        SetupConfigurationOwnership.CopyOwnedProperties(setupState.Configuration, liveConfiguration);
        //The detailed settings pages are validated by the controller filter before they reach this service. The
        //assistant posts a whole setup state instead, so nothing validated this on the way in and it has to happen
        //here - otherwise setup is the one route that can store a combination the app itself rejects.
        await Validate(baseConfigurationValidator, liveConfiguration).ConfigureAwait(false);
        await baseConfigurationService.UpdateBaseConfigurationAsync(liveConfiguration).ConfigureAwait(false);
    }

    private async Task SaveChargePrice(DtoSetupState setupState)
    {
        //Nothing to store until the user has said what they pay. Saving a half answered tariff would be worse than
        //waiting for it: every charging decision is made against this number.
        if (setupState.ChargePrice?.GridPrice is not > 0)
        {
            return;
        }

        //Derived onto a copy rather than onto the answers themselves. Writing the stored form back into the state
        //would change the state every time it is applied, and a retry could then never tell "already done" from
        //"the user has edited this since".
        var priceToSave = new DtoChargePrice
        {
            Id = setupState.ChargePrice.Id,
            ValidSince = setupState.ChargePrice.ValidSince,
            GridPrice = setupState.ChargePrice.GridPrice,
            //A household without panels exports nothing, so there is no value to put on it. The stored price cannot
            //hold "none", so the absence is written as zero rather than left to fail on the way in.
            SolarPrice = setupState.ChargePrice.SolarPrice ?? 0,
            AddSpotPriceToGridPrice = setupState.ChargePrice.AddSpotPriceToGridPrice,
            SpotPriceRegion = setupState.ChargePrice.SpotPriceRegion,
            SpotPriceSurcharge = setupState.ChargePrice.SpotPriceSurcharge,
            //The periods of a time of use tariff are edited as a list but stored as the serialized configuration of
            //the price. Writing the price without them would keep the tariff the user just replaced; clearing it
            //for the kinds that have no periods is what stops yesterday's periods staying in force after a change.
            EnergyProviderConfiguration =
                setupState.ResolvedElectricityPriceKind == SetupElectricityPriceKind.TimeOfUse && setupState.FixedPrices.Count > 0
                    ? JsonConvert.SerializeObject(setupState.FixedPrices)
                    : null,
        };
        //Same rule the detailed tariff page is held to. Without this setup was the one route that could store a
        //market tariff with no region, which no price can then be fetched for.
        await Validate(chargePriceValidator, priceToSave).ConfigureAwait(false);
        await chargingCostService.UpdateChargePrice(priceToSave).ConfigureAwait(false);
    }

    private async Task SaveCarDraft(DtoSetupCarDraft draft)
    {
        await AssignChargingPriority(draft).ConfigureAwait(false);
        var configuration = CloneForDraftSave(draft.Configuration);
        //Saving during setup must never hand new equipment to the charging scheduler; that happens when setup
        //finishes. A car that was already charging before the assistant opened is a different matter: it is part of
        //a working installation, and switching it off to save an unrelated answer would stop it charging and drop
        //its charging connector assignments.
        configuration.ShouldBeManaged = draft.WasManagedBeforeSetup;
        await SaveCarConfiguration(draft, configuration).ConfigureAwait(false);

        if (draft.CarId == null)
        {
            //A freshly created car has no id until it is saved. Look it up by VIN so the draft keeps pointing at
            //the same row and a retry updates it instead of creating a second car.
            var createdCar = await teslaSolarChargerContext.Cars
                .Where(c => c.Vin == configuration.Vin)
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync().ConfigureAwait(false);
            draft.CarId = createdCar?.Id;
            //The id belongs on the configuration too, not only on the draft. The next save passes the configuration
            //straight through to the in memory car, and a zero there would renumber a car that is already running.
            draft.Configuration.Id = createdCar?.Id ?? draft.Configuration.Id;
        }
    }

    private async Task ActivateCar(DtoSetupCarDraft draft)
    {
        if (draft.CarId == null)
        {
            throw new InvalidOperationException("The car was not saved, so it cannot be activated.");
        }

        await AssignChargingPriority(draft).ConfigureAwait(false);
        var configuration = CloneForDraftSave(draft.Configuration);
        configuration.Id = draft.CarId.Value;
        configuration.ShouldBeManaged = true;
        await SaveCarConfiguration(draft, configuration).ConfigureAwait(false);
        await ApplyConnectorAssignments(draft).ConfigureAwait(false);
    }

    /// <summary>
    /// Gives a car without a charging priority the next place after every other car, so cars are served in the order
    /// they were set up and nobody has to be asked. A car that already has a place keeps it, including one the draft
    /// has not heard about yet because it was written by an earlier save.
    /// </summary>
    private async Task AssignChargingPriority(DtoSetupCarDraft draft)
    {
        if (draft.Configuration.ChargingPriority > 0)
        {
            return;
        }

        var carId = draft.CarId;
        var storedPriority = carId == null
            ? 0
            : await teslaSolarChargerContext.Cars
                .Where(c => c.Id == carId.Value)
                .Select(c => c.ChargingPriority)
                .FirstOrDefaultAsync().ConfigureAwait(false);
        if (storedPriority > 0)
        {
            draft.Configuration.ChargingPriority = storedPriority;
            return;
        }

        var highestOtherPriority = await teslaSolarChargerContext.Cars
            .Where(c => carId == null || c.Id != carId.Value)
            .Select(c => (int?)c.ChargingPriority)
            .MaxAsync().ConfigureAwait(false) ?? 0;
        draft.Configuration.ChargingPriority = Math.Max(highestOtherPriority, 0) + 1;
    }

    /// <summary>
    /// Validates and writes one car. Every rule the car validator has is written for a managed car, so a draft
    /// saved switched off passes trivially and only the activating save is really checked - which is exactly the
    /// moment the car starts being charged by these values.
    /// </summary>
    private async Task SaveCarConfiguration(DtoSetupCarDraft draft, CarBasicConfiguration configuration)
    {
        await Validate(carConfigurationValidator, configuration).ConfigureAwait(false);
        await configJsonService.UpdateCarBasicConfiguration(draft.CarId ?? default, configuration).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs one of the app's own validators and reports a failure the way the rest of the assistant expects: a
    /// <see cref="ValidationException"/> is what marks an operation as something to change rather than retry.
    /// </summary>
    private static async Task Validate<T>(IValidator<T> validator, T instance)
    {
        var validationResult = await validator.ValidateAsync(instance).ConfigureAwait(false);
        if (validationResult.IsValid)
        {
            return;
        }

        throw new ValidationException(string.Join(Environment.NewLine,
            validationResult.Errors.Select(e => e.ErrorMessage).Distinct()), validationResult.Errors);
    }

    /// <summary>
    /// Makes the stored assignments match the ones the user made while the car was still a draft. Applied after the
    /// car is managed, because an unmanaged car is deliberately removed from every connector's allowed cars.
    /// Unticking a connector has to remove it as well: leaving it in would keep the car charging somewhere the user
    /// just said it does not belong.
    /// </summary>
    private async Task ApplyConnectorAssignments(DtoSetupCarDraft draft)
    {
        if (draft.CarId == null)
        {
            return;
        }

        var existingAssignments = await teslaSolarChargerContext.ChargingStationConnectorAllowedCars
            .Where(a => a.CarId == draft.CarId.Value)
            .ToListAsync().ConfigureAwait(false);

        var staleAssignments = existingAssignments
            .Where(a => !draft.AssignedChargingConnectorIds.Contains(a.OcppChargingStationConnectorId))
            .ToList();
        teslaSolarChargerContext.ChargingStationConnectorAllowedCars.RemoveRange(staleAssignments);

        foreach (var connectorId in draft.AssignedChargingConnectorIds)
        {
            if (existingAssignments.Any(a => a.OcppChargingStationConnectorId == connectorId))
            {
                continue;
            }

            teslaSolarChargerContext.ChargingStationConnectorAllowedCars.Add(new ChargingStationConnectorAllowedCar
            {
                CarId = draft.CarId.Value,
                OcppChargingStationConnectorId = connectorId,
            });
        }

        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task ActivateChargingStationConnector(DtoSetupChargerDraft draft)
    {
        if (draft.ConnectorId == null)
        {
            throw new InvalidOperationException("The charging station connector has not connected yet, so it cannot be activated.");
        }

        var connector = await teslaSolarChargerContext.OcppChargingStationConnectors
            .FirstOrDefaultAsync(c => c.Id == draft.ConnectorId.Value).ConfigureAwait(false);
        if (connector == null)
        {
            throw new InvalidOperationException("The charging station connector no longer exists.");
        }

        connector.ShouldBeManaged = true;
        connector.AllowGuestCars = draft.AllowGuestCars;
        //Setup does not ask for the lowest current: almost no car charges below 6 A, and a managed connector must
        //have one. A value set earlier, for example on the charging stations page, is kept.
        connector.MinCurrent ??= DefaultChargingStationMinCurrent;
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task CompleteSetup()
    {
        var liveConfiguration = await configurationWrapper.GetBaseConfigurationAsync().ConfigureAwait(false);
        liveConfiguration.IsFirstRun = false;
        await baseConfigurationService.UpdateBaseConfigurationAsync(liveConfiguration).ConfigureAwait(false);
    }

    /// <summary>
    /// A shallow copy, so forcing <see cref="CarBasicConfiguration.ShouldBeManaged"/> for a save does not change the
    /// draft the user is still editing.
    /// </summary>
    private static CarBasicConfiguration CloneForDraftSave(CarBasicConfiguration source)
    {
        var clone = new CarBasicConfiguration();
        foreach (var property in typeof(CarBasicConfiguration).GetProperties().Where(p => p is { CanRead: true, CanWrite: true }))
        {
            property.SetValue(clone, property.GetValue(source));
        }

        return clone;
    }

    private bool TrySetProperty(object target, string propertyName, object? value)
    {
        var property = target.GetType().GetProperty(propertyName);
        if (property is not { CanWrite: true })
        {
            logger.LogWarning("Proposed property {propertyName} does not exist on {type} and is ignored.", propertyName, target.GetType().Name);
            return false;
        }

        try
        {
            //The value travelled through JSON, so it arrives as a JSON primitive rather than the target type.
            //JToken conversion handles numbers, booleans and enum names alike.
            property.SetValue(target, value == null ? null : JToken.FromObject(value).ToObject(property.PropertyType));
            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Proposed value for {propertyName} could not be applied and is ignored.", propertyName);
            return false;
        }
    }
}
