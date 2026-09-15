using Newtonsoft.Json;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Setup;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class SetupStateService(
    ILogger<SetupStateService> logger,
    ITscConfigurationService tscConfigurationService,
    ISetupStateMigrator setupStateMigrator,
    IConfigurationWrapper configurationWrapper,
    IConfigJsonService configJsonService,
    IDateTimeProvider dateTimeProvider,
    IConstants constants)
    : ISetupStateService
{
    public async Task<DtoSetupState?> GetSetupState()
    {
        logger.LogTrace("{method}()", nameof(GetSetupState));
        var persistedJson = await tscConfigurationService.GetConfigurationValueByKey(constants.SetupCacheKey);
        var migrated = setupStateMigrator.Migrate(persistedJson);
        if (migrated == null)
        {
            return null;
        }

        //Persist the migrated shape right away, so the migration runs once instead of on every read and a later
        //write cannot accidentally mix the old and the new shape.
        if (!string.Equals(persistedJson, JsonConvert.SerializeObject(migrated), StringComparison.Ordinal))
        {
            await PersistState(migrated).ConfigureAwait(false);
        }

        return migrated;
    }

    public async Task<DtoSetupState> GetOrCreateSetupState()
    {
        logger.LogTrace("{method}()", nameof(GetOrCreateSetupState));
        var existing = await GetSetupState().ConfigureAwait(false);
        if (existing != null)
        {
            return existing;
        }

        var state = new DtoSetupState
        {
            Configuration = await configurationWrapper.GetBaseConfigurationAsync().ConfigureAwait(false),
        };

        //Everything already configured is an explicit choice of this installation, not something the assistant may
        //replace with a newly proposed default.
        foreach (var propertyName in SetupConfigurationOwnership.OwnedProperties)
        {
            state.ValueSources[propertyName] = SetupValueSource.ExistingConfiguration;
        }

        await SeedDraftsFromExistingCars(state).ConfigureAwait(false);
        return state;
    }

    public async Task<DtoSetupState> SyncCarDrafts(DtoSetupState setupState)
    {
        logger.LogTrace("{method}(...)", nameof(SyncCarDrafts));
        List<Shared.Dtos.CarBasicConfiguration> existingCars;
        try
        {
            existingCars = await configJsonService.GetCarBasicConfigurations().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            //Leave the state exactly as it was: showing fewer cars is better than dropping the drafts the user has
            //already worked on because one lookup failed.
            logger.LogWarning(exception, "Could not load existing cars while syncing the setup drafts.");
            return setupState;
        }

        var existingCarIds = existingCars.Select(c => c.Id).ToHashSet();
        //A draft whose car was deleted elsewhere would otherwise keep asking to be finished.
        setupState.CarDrafts.RemoveAll(d => d.CarId != null && !existingCarIds.Contains(d.CarId.Value));

        foreach (var car in existingCars.Where(c => setupState.CarDrafts.All(d => d.CarId != c.Id)))
        {
            setupState.CarDrafts.Add(CreateDraft(car));
        }

        await PersistState(setupState).ConfigureAwait(false);
        return setupState;
    }

    public async Task UpdateSetupState(DtoSetupState setupState)
    {
        logger.LogTrace("{method}(...)", nameof(UpdateSetupState));
        //A client may post a state it read before this version shipped. Stamping it keeps the persisted schema
        //version honest about the shape that is actually written.
        setupState.SchemaVersion = DtoSetupState.CurrentSchemaVersion;
        await PersistState(setupState).ConfigureAwait(false);
    }

    public async Task DeleteSetupState()
    {
        logger.LogTrace("{method}()", nameof(DeleteSetupState));
        await tscConfigurationService.SetConfigurationValueByKey(constants.SetupCacheKey, string.Empty).ConfigureAwait(false);
    }

    private async Task PersistState(DtoSetupState setupState)
    {
        setupState.LastSavedAt = dateTimeProvider.DateTimeOffSetUtcNow();
        var json = JsonConvert.SerializeObject(setupState);
        await tscConfigurationService.SetConfigurationValueByKey(constants.SetupCacheKey, json).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a draft for every car that already exists, so an imported Tesla or SmartCar continues directly into
    /// configuration instead of sitting in a list waiting to be edited. The draft carries the car's id, so applying
    /// it updates that car rather than creating a second one.
    /// </summary>
    private async Task SeedDraftsFromExistingCars(DtoSetupState state)
    {
        List<Shared.Dtos.CarBasicConfiguration> existingCars;
        try
        {
            existingCars = await configJsonService.GetCarBasicConfigurations().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            //Not being able to list the cars must not stop the assistant from opening: the user can still configure
            //everything else and the cars are picked up the next time the state is created.
            logger.LogWarning(exception, "Could not load existing cars while creating the setup state.");
            return;
        }

        foreach (var car in existingCars)
        {
            state.CarDrafts.Add(CreateDraft(car));
        }
    }

    private static DtoSetupCarDraft CreateDraft(Shared.Dtos.CarBasicConfiguration car)
    {
        var route = DeriveConnectionRoute(car);
        return new DtoSetupCarDraft
        {
            CarId = car.Id,
            Configuration = car,
            //A car that is already managed is part of a working installation. Setup must be able to describe it
            //without implying it is about to be switched on for the first time.
            ShouldBeActivated = car.ShouldBeManaged,
            ConnectionRoute = route,
            //A car that arrived with a known name and identification number has nothing left to identify, so start
            //it where there is actually something to do.
            Stage = string.IsNullOrWhiteSpace(car.Vin) || string.IsNullOrWhiteSpace(car.Name)
                ? SetupCarStage.Identify
                : route == SetupCarConnectionRoute.Undecided
                    ? SetupCarStage.Connection
                    : SetupCarStage.Connect,
        };
    }

    /// <summary>
    /// Reads back which route an existing car was configured for, so the assistant describes what is actually set
    /// up instead of asking the user to choose again.
    /// </summary>
    private static SetupCarConnectionRoute DeriveConnectionRoute(Shared.Dtos.CarBasicConfiguration car)
    {
        if (car.CarType == CarType.Tesla)
        {
            if (car.UseBle)
            {
                return SetupCarConnectionRoute.TeslaBluetooth;
            }

            return car.UseFleetTelemetry ? SetupCarConnectionRoute.TeslaCloud : SetupCarConnectionRoute.Undecided;
        }

        return car.CarType switch
        {
            CarType.SmartCar => SetupCarConnectionRoute.SmartCarWithChargingStation,
            //A manual car has no data connection of its own, so a charging station is the only thing that can
            //control it and supply what we know about its charge level.
            CarType.Manual => SetupCarConnectionRoute.ChargingStationOnly,
            _ => SetupCarConnectionRoute.Undecided,
        };
    }
}
