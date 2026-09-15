using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using TeslaSolarCharger.Model.Contracts;
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
    ITeslaSolarChargerContext teslaSolarChargerContext,
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

        //On an installation that has been set up before, what is stored is what that installation decided - a
        //forecast switched off on purpose reads exactly like one never switched on, so the assistant must not offer
        //to turn it back on as a recommendation. On a first run the same values are only untouched defaults, and
        //treating those as decisions would leave a beginner with nothing proposed at all.
        if (!state.Configuration.IsFirstRun)
        {
            foreach (var owned in SetupConfigurationOwnership.OwnedValues(state.Configuration))
            {
                //A setting that can be null and is says so plainly: nobody has answered it, not even by leaving it
                //alone. Those stay open to a recommendation however long the installation has been running.
                if (owned.Value == null)
                {
                    continue;
                }

                state.ValueSources[owned.Key] = SetupValueSource.ExistingConfiguration;
            }
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

        //A draft that is already open has to pick up what happened outside the assistant. Authorizing SmartCar, for
        //one, changes the car's type in the database; a draft still saying "manual" would keep offering to connect
        //what is already connected and would write the old type back on the next save.
        foreach (var draft in setupState.CarDrafts.Where(d => d.CarId != null))
        {
            var car = existingCars.FirstOrDefault(c => c.Id == draft.CarId);
            if (car != null)
            {
                RefreshConnectionState(draft, car);
            }
        }

        foreach (var car in existingCars.Where(c => setupState.CarDrafts.All(d => d.CarId != c.Id)))
        {
            setupState.CarDrafts.Add(await CreateDraftWithAssignments(car).ConfigureAwait(false));
        }

        await PersistState(setupState).ConfigureAwait(false);
        return setupState;
    }

    /// <summary>
    /// Copies the parts of a car that something other than the assistant owns onto an open draft, and leaves every
    /// answer the user typed alone. Which service a car is connected to is decided by an authorization the user
    /// completes outside setup, so it is read back rather than remembered.
    /// </summary>
    private static void RefreshConnectionState(DtoSetupCarDraft draft, Shared.Dtos.CarBasicConfiguration car)
    {
        if (draft.Configuration.CarType == car.CarType
            && draft.Configuration.ShouldBeManaged == car.ShouldBeManaged)
        {
            return;
        }

        draft.Configuration.CarType = car.CarType;
        //Whether the car is actually running is the installation's state, not an answer in the assistant. Keeping a
        //stale value here would make a running car look like a draft waiting to be switched on.
        draft.Configuration.ShouldBeManaged = car.ShouldBeManaged;
        draft.WasManagedBeforeSetup = car.ShouldBeManaged;
        //A route derived from the old type no longer describes the car. Only fill in a route that follows from what
        //the car now is, so a user who deliberately picked one of several Tesla routes keeps their choice.
        var route = DeriveConnectionRoute(car);
        if (route != SetupCarConnectionRoute.Undecided)
        {
            draft.ConnectionRoute = route;
        }
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
            state.CarDrafts.Add(await CreateDraftWithAssignments(car).ConfigureAwait(false));
        }
    }

    /// <summary>
    /// Creates a draft and fills in the charging connectors this car is already allowed on. Without them the draft
    /// would describe the car as assigned to nothing, and applying it would take away assignments the user made
    /// long before the assistant was opened.
    /// </summary>
    private async Task<DtoSetupCarDraft> CreateDraftWithAssignments(Shared.Dtos.CarBasicConfiguration car)
    {
        var draft = CreateDraft(car);
        try
        {
            draft.AssignedChargingConnectorIds = await teslaSolarChargerContext.ChargingStationConnectorAllowedCars
                .Where(a => a.CarId == car.Id)
                .Select(a => a.OcppChargingStationConnectorId)
                .ToListAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            //Opening the assistant matters more than this list, which the user can still set on the car's review
            //screen. It is only read here so they do not have to.
            logger.LogWarning(exception, "Could not read the charging connectors car {carId} is allowed on.", car.Id);
        }

        return draft;
    }

    private static DtoSetupCarDraft CreateDraft(Shared.Dtos.CarBasicConfiguration car)
    {
        var route = DeriveConnectionRoute(car);
        return new DtoSetupCarDraft
        {
            CarId = car.Id,
            Configuration = car,
            //Read from the database, which is the only place that knows whether this car is actually charging today.
            WasManagedBeforeSetup = car.ShouldBeManaged,
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
