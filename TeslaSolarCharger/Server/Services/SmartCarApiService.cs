using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class SmartCarApiService : ISmartCarApiService
{
    private readonly ILogger<SmartCarApiService> _logger;
    private readonly ITokenHelper _tokenHelper;
    private readonly ITeslaSolarChargerContext _teslaSolarChargerContext;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public SmartCarApiService(ILogger<SmartCarApiService> logger,
        ITokenHelper tokenHelper,
        ITeslaSolarChargerContext teslaSolarChargerContext,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _tokenHelper = tokenHelper;
        _teslaSolarChargerContext = teslaSolarChargerContext;
        _serviceScopeFactory = serviceScopeFactory;
    }

    // Name given to a freshly created SmartCar car until SmartCar delivers the VIN. Used to decide
    // whether the auto-generated name may be overwritten with the VIN on backfill (a user rename is kept).
    private const string PendingCarName = "New SmartCar";

    public async Task UpdateSmartCarCarTypes(bool forceRefresh = false)
    {
        _logger.LogTrace("{method}({forceRefresh})", nameof(UpdateSmartCarCarTypes), forceRefresh);
        try
        {
            var dbCars = await _teslaSolarChargerContext.Cars.ToListAsync().ConfigureAwait(false);

            // While a SmartCar car is still waiting for its VIN (placeholder), poll uncached so the VIN
            // backfills within one cycle of the webhook instead of waiting for the cached state to expire.
            var hasPendingPlaceholder = dbCars.Any(c => c.CarType == CarType.SmartCar
                                                        && !string.IsNullOrEmpty(c.SmartCarVehicleId)
                                                        && string.IsNullOrEmpty(c.Vin));
            var useCache = !forceRefresh && !hasPendingPlaceholder;

            var tokens = await _tokenHelper.GetSmartCarTokenStates(useCache).ConfigureAwait(false);

            // Deduplicate connections by vehicle id (a vehicle could in theory surface under multiple
            // tokens), preferring an entry that already carries a VIN.
            var connections = tokens
                .SelectMany(t => t.Connections)
                .Where(c => !string.IsNullOrEmpty(c.SmartCarVehicleId))
                .GroupBy(c => c.SmartCarVehicleId)
                .Select(g => g.FirstOrDefault(c => !string.IsNullOrEmpty(c.Vin)) ?? g.First())
                .ToList();

            var connectedVehicleIds = connections.Select(c => c.SmartCarVehicleId).ToHashSet();
            var connectedVins = connections.Where(c => !string.IsNullOrEmpty(c.Vin)).Select(c => c.Vin!).ToHashSet();
            var hasPendingConnections = tokens.Any(t => t.HasPendingConnections) || connections.Any(c => string.IsNullOrEmpty(c.Vin));
            _logger.LogTrace("Found {count} SmartCar connections. VINs: {vins}. HasPending: {hasPendingConnections}",
                connections.Count, connectedVins, hasPendingConnections);

            var changed = false;
            foreach (var connection in connections)
            {
                // 1. Match by the stable vehicle id.
                var dbCar = dbCars.FirstOrDefault(c => c.SmartCarVehicleId == connection.SmartCarVehicleId);

                // 2. Legacy fallback: an existing car already keyed by VIN (created before vehicle ids were
                //    tracked). Adopt the vehicle id onto it.
                if (dbCar == default && !string.IsNullOrEmpty(connection.Vin))
                {
                    dbCar = dbCars.FirstOrDefault(c => c.Vin == connection.Vin);
                    if (dbCar != default)
                    {
                        dbCar.SmartCarVehicleId = connection.SmartCarVehicleId;
                        changed = true;
                    }
                }

                // 3. Still nothing: create a placeholder keyed on the vehicle id (VIN may still be null).
                if (dbCar == default)
                {
                    _logger.LogInformation("Creating new SmartCar car for vehicle id {vehicleId} (VIN {vin})",
                        connection.SmartCarVehicleId, connection.Vin);
                    dbCar = CreateSmartCarCar(connection.SmartCarVehicleId, connection.Vin, dbCars);
                    _teslaSolarChargerContext.Cars.Add(dbCar);
                    dbCars.Add(dbCar);
                    changed = true;
                }

                if (dbCar.CarType != CarType.SmartCar)
                {
                    dbCar.CarType = CarType.SmartCar;
                    changed = true;
                }

                // 4. Backfill / reconcile the VIN once SmartCar has delivered it.
                if (!string.IsNullOrEmpty(connection.Vin) && dbCar.Vin != connection.Vin)
                {
                    // Another row already holds this VIN (e.g. a legacy SmartCar car, or the same physical
                    // car reconnected under a new vehicle id). Keep that established row and drop the
                    // placeholder, otherwise the unique VIN index would be violated.
                    var existingWithVin = dbCars.FirstOrDefault(c => c != dbCar && c.Vin == connection.Vin);
                    if (existingWithVin != default)
                    {
                        // Delete the placeholder and flush first so its (now duplicate) vehicle id is freed
                        // before we move it onto the surviving row — EF does not guarantee that the delete
                        // runs before the update within a single SaveChanges, which would trip the unique index.
                        _teslaSolarChargerContext.Cars.Remove(dbCar);
                        dbCars.Remove(dbCar);
                        await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);

                        existingWithVin.SmartCarVehicleId = connection.SmartCarVehicleId;
                        if (existingWithVin.CarType != CarType.SmartCar)
                        {
                            existingWithVin.CarType = CarType.SmartCar;
                        }
                        dbCar = existingWithVin;
                    }
                    else
                    {
                        dbCar.Vin = connection.Vin;
                        if (string.IsNullOrEmpty(dbCar.Name) || dbCar.Name == PendingCarName)
                        {
                            dbCar.Name = connection.Vin;
                        }
                    }
                    changed = true;
                }
            }

            // Revert cars that are no longer connected back to manual.
            foreach (var smartCarCar in dbCars.Where(c => c.CarType == CarType.SmartCar))
            {
                var connectedByVehicleId = !string.IsNullOrEmpty(smartCarCar.SmartCarVehicleId)
                                           && connectedVehicleIds.Contains(smartCarCar.SmartCarVehicleId);
                var connectedByVin = !string.IsNullOrEmpty(smartCarCar.Vin) && connectedVins.Contains(smartCarCar.Vin);
                if (connectedByVehicleId || connectedByVin)
                {
                    continue;
                }

                // An identity-less SmartCar car can never be reconciled, so always demote it. Otherwise only
                // demote when there are no pending connections (avoid demoting a car whose VIN is still loading).
                var hasNoIdentity = string.IsNullOrEmpty(smartCarCar.SmartCarVehicleId) && string.IsNullOrEmpty(smartCarCar.Vin);
                if (hasNoIdentity || !hasPendingConnections)
                {
                    smartCarCar.CarType = CarType.Manual;
                    // Release the vehicle id so a future reconnect can claim it without hitting the unique index.
                    smartCarCar.SmartCarVehicleId = null;
                    changed = true;
                }
            }

            await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);

            if (changed)
            {
                // Mirror the created/updated cars into the runtime settings so they are managed immediately.
                using var scope = _serviceScopeFactory.CreateScope();
                var configJsonService = scope.ServiceProvider.GetRequiredService<IConfigJsonService>();
                await configJsonService.AddCarsToSettings(null).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not get SmartCar token states");
        }

    }

    private static Car CreateSmartCarCar(string smartCarVehicleId, string? vin, List<Car> existingCars)
    {
        var highestChargingPriority = existingCars.Any() ? existingCars.Max(c => c.ChargingPriority) : 0;
        return new Car
        {
            SmartCarVehicleId = smartCarVehicleId,
            Vin = string.IsNullOrEmpty(vin) ? null : vin,
            Name = string.IsNullOrEmpty(vin) ? PendingCarName : vin,
            CarType = CarType.SmartCar,
            ChargeMode = ChargeModeV2.Auto,
            ShouldBeManaged = false,
            MinimumSoc = 10,
            MaximumSoc = 100,
            MinimumAmpere = 6,
            MaximumAmpere = 16,
            MaximumPhases = 1,
            UsableEnergy = 75,
            ChargingPriority = highestChargingPriority + 1,
        };
    }
}
