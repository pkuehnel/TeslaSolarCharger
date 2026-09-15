using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
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
    IDeferredSetupCheckService deferredSetupCheckService)
    : ISetupApplicationService
{
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
        await ApplyConfigurationInternal(setupState, result).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            //Activating equipment whose configuration did not save would switch on something other than what the
            //user reviewed, so stop here and keep the state for a retry.
            return result;
        }

        foreach (var draft in setupState.CarDrafts.Where(d => d.ShouldBeActivated))
        {
            await RunOperation(setupState, result, SetupOperationKey.ActivateCar, draft.DraftId,
                () => ActivateCar(draft)).ConfigureAwait(false);
        }

        foreach (var draft in setupState.ChargerDrafts.Where(d => d.ShouldBeActivated && d.ConnectorId != null))
        {
            await RunOperation(setupState, result, SetupOperationKey.ActivateChargingStationConnector, draft.DraftId,
                () => ActivateChargingStationConnector(draft)).ConfigureAwait(false);
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
        await RecordOutstandingChargingTests(setupState).ConfigureAwait(false);
        await setupStateService.DeleteSetupState().ConfigureAwait(false);
        return result;
    }

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

        //An explicit save means the user changed something, so a record of an earlier save must not make this one a
        //no-op. Idempotency protects a retried finish, not an edit.
        setupState.CompletedOperations.RemoveAll(o => o.OperationKey == SetupOperationKey.SaveCarDraft && o.DraftId == draftId);
        await RunOperation(setupState, result, SetupOperationKey.SaveCarDraft, draftId,
            () => SaveCarDraft(draft)).ConfigureAwait(false);
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

    /// <summary>
    /// Notes, for every piece of equipment that was just switched on, that nobody has seen it actually charge yet.
    /// Kept outside the setup state so finishing does not erase it, and left to resolve itself the first time the
    /// equipment really charges rather than asking the user to run a test on the spot.
    /// </summary>
    private async Task RecordOutstandingChargingTests(DtoSetupState setupState)
    {
        foreach (var draft in setupState.CarDrafts.Where(d => d.ShouldBeActivated && d.CarId != null))
        {
            await AddChargingTest(SetupDeviceKind.Car, draft.CarId!.Value, draft.Configuration.Name,
                DescribeCarSetup(draft)).ConfigureAwait(false);
        }

        foreach (var draft in setupState.ChargerDrafts.Where(d => d.ShouldBeActivated && d.ConnectorId != null))
        {
            await AddChargingTest(SetupDeviceKind.ChargingStationConnector, draft.ConnectorId!.Value,
                draft.DisplayName ?? draft.ChargepointId, draft.ChargepointId).ConfigureAwait(false);
        }
    }

    private async Task AddChargingTest(SetupDeviceKind deviceKind, int deviceId, string? displayName, string? fingerprint)
    {
        try
        {
            await deferredSetupCheckService.AddOrUpdateDeferredCheck(new DtoDeferredSetupCheck
            {
                Kind = DeferredSetupCheckKind.RealChargingTest,
                DeviceKind = deviceKind,
                DeviceId = deviceId,
                DisplayName = displayName,
                ConfigurationFingerprint = fingerprint,
            }).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            //Not being able to note the outstanding test is not a reason to fail a setup that otherwise worked.
            logger.LogWarning(exception, "Could not record the outstanding charging test for {deviceKind} {deviceId}.",
                deviceKind, deviceId);
        }
    }

    /// <summary>
    /// What this car's charging depends on. When any of it changes, the recorded result no longer describes what is
    /// configured, and the check starts again.
    /// </summary>
    private static string DescribeCarSetup(DtoSetupCarDraft draft) =>
        string.Join('|',
            draft.ConnectionRoute,
            draft.Configuration.UseBle,
            draft.Configuration.UseFleetTelemetry,
            draft.Configuration.BleApiBaseUrl ?? string.Empty,
            string.Join(',', draft.AssignedChargingConnectorIds.OrderBy(id => id)));

    private async Task ApplyConfigurationInternal(DtoSetupState setupState, DtoSetupApplicationResult result)
    {
        //The base configuration goes first on purpose. It carries the installation wide switches a car's own
        //validation reads - reading battery levels over Bluetooth above all - so saving a car before them would
        //fail on a setting the user already answered inside the assistant.
        await RunOperation(setupState, result, SetupOperationKey.SaveBaseConfiguration, null,
            () => SaveBaseConfiguration(setupState)).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return;
        }

        await RunOperation(setupState, result, SetupOperationKey.SaveChargePrice, null,
            () => SaveChargePrice(setupState)).ConfigureAwait(false);

        foreach (var draft in setupState.CarDrafts)
        {
            await RunOperation(setupState, result, SetupOperationKey.SaveCarDraft, draft.DraftId,
                () => SaveCarDraft(draft)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Runs one application step unless a previous attempt already completed it, records the outcome on the state
    /// and reports it. Recording as we go is what makes a retry after a partial failure safe.
    /// </summary>
    private async Task RunOperation(DtoSetupState setupState,
        DtoSetupApplicationResult result,
        SetupOperationKey operationKey,
        Guid? draftId,
        Func<Task> operation)
    {
        if (setupState.CompletedOperations.Any(o => o.OperationKey == operationKey && o.DraftId == draftId))
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

        setupState.CompletedOperations.Add(new DtoSetupOperationRecord
        {
            OperationKey = operationKey,
            DraftId = draftId,
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
        await baseConfigurationService.UpdateBaseConfigurationAsync(liveConfiguration).ConfigureAwait(false);
    }

    private async Task SaveChargePrice(DtoSetupState setupState)
    {
        if (setupState.ChargePrice == null)
        {
            return;
        }

        await chargingCostService.UpdateChargePrice(setupState.ChargePrice).ConfigureAwait(false);
    }

    private async Task SaveCarDraft(DtoSetupCarDraft draft)
    {
        var configuration = CloneForDraftSave(draft.Configuration);
        //Saving during setup must never make a car available to the charging scheduler. Activation is its own,
        //explicit step at the end of the assistant.
        configuration.ShouldBeManaged = false;
        await configJsonService.UpdateCarBasicConfiguration(draft.CarId ?? default, configuration).ConfigureAwait(false);

        if (draft.CarId == null)
        {
            //A freshly created car has no id until it is saved. Look it up by VIN so the draft keeps pointing at
            //the same row and a retry updates it instead of creating a second car.
            var createdCar = await teslaSolarChargerContext.Cars
                .Where(c => c.Vin == configuration.Vin)
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync().ConfigureAwait(false);
            draft.CarId = createdCar?.Id;
        }
    }

    private async Task ActivateCar(DtoSetupCarDraft draft)
    {
        if (draft.CarId == null)
        {
            throw new InvalidOperationException("The car was not saved, so it cannot be activated.");
        }

        var configuration = CloneForDraftSave(draft.Configuration);
        configuration.Id = draft.CarId.Value;
        configuration.ShouldBeManaged = true;
        await configJsonService.UpdateCarBasicConfiguration(draft.CarId.Value, configuration).ConfigureAwait(false);
        await ApplyConnectorAssignments(draft).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the connector assignments the user made while the car was still a draft. They are applied after the
    /// car is managed, because an unmanaged car is deliberately removed from every connector's allowed cars.
    /// </summary>
    private async Task ApplyConnectorAssignments(DtoSetupCarDraft draft)
    {
        if (draft.CarId == null || draft.AssignedChargingConnectorIds.Count == 0)
        {
            return;
        }

        var existingAssignments = await teslaSolarChargerContext.ChargingStationConnectorAllowedCars
            .Where(a => a.CarId == draft.CarId.Value)
            .ToListAsync().ConfigureAwait(false);

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
