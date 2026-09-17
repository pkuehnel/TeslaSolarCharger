using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class SmartCarApiService : ISmartCarApiService
{
    private readonly ILogger<SmartCarApiService> _logger;
    private readonly ITokenHelper _tokenHelper;
    private readonly ITeslaSolarChargerContext _teslaSolarChargerContext;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SmartCarApiService(ILogger<SmartCarApiService> logger,
        ITokenHelper tokenHelper,
        ITeslaSolarChargerContext teslaSolarChargerContext,
        IServiceScopeFactory serviceScopeFactory,
        IMemoryCache memoryCache,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _tokenHelper = tokenHelper;
        _teslaSolarChargerContext = teslaSolarChargerContext;
        _serviceScopeFactory = serviceScopeFactory;
        _memoryCache = memoryCache;
        _dateTimeProvider = dateTimeProvider;
    }

    // Placeholder name used only when SmartCar has not yet reported the make/model of a freshly connected car.
    // It is replaced by the make/model name on a later sync; a name the user already set is kept (never
    // overwritten, and the VIN is never used as the name).
    private const string PendingCarName = "New SmartCar";

    // How long after first seeing a VIN-less placeholder we keep polling uncached so the VIN backfills quickly.
    // After this window we fall back to the normal cached poll, otherwise a placeholder that never receives a
    // VIN would force an uncached backend call on every cycle forever.
    private static readonly TimeSpan PlaceholderFastRefreshWindow = TimeSpan.FromMinutes(15);
    private const string PlaceholderFirstSeenCacheKeyPrefix = "SmartCarPlaceholderFirstSeen_";

    public async Task UpdateSmartCarCarTypes(bool forceRefresh = false)
    {
        _logger.LogTrace("{method}({forceRefresh})", nameof(UpdateSmartCarCarTypes), forceRefresh);
        try
        {
            var dbCars = await _teslaSolarChargerContext.Cars.ToListAsync().ConfigureAwait(false);

            // While a SmartCar car is still waiting for its VIN (placeholder), poll uncached so the VIN
            // backfills within one cycle of the webhook instead of waiting for the cached state to expire.
            // Only do this for placeholders that are still inside their fast-refresh window (see below) so a
            // placeholder that never receives a VIN does not force an uncached backend call on every cycle.
            var pendingPlaceholders = dbCars.Where(c => c.CarType == CarType.SmartCar
                                                        && !string.IsNullOrEmpty(c.SmartCarVehicleId)
                                                        && string.IsNullOrEmpty(c.Vin));
            var hasPendingPlaceholder = false;
            foreach (var placeholder in pendingPlaceholders)
            {
                // Evaluate every placeholder (do not short circuit) so each one's first-seen time is recorded.
                if (IsPlaceholderWithinFastRefreshWindow(placeholder.SmartCarVehicleId!))
                {
                    hasPendingPlaceholder = true;
                }
            }
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

                // 3. Still nothing: create a placeholder keyed on the vehicle id (VIN may still be null). It is
                //    named from the make/model when SmartCar reports them, otherwise with a placeholder name.
                if (dbCar == default)
                {
                    _logger.LogInformation("Creating new SmartCar car for vehicle id {vehicleId} (VIN {vin}, {make} {model})",
                        connection.SmartCarVehicleId, connection.Vin, connection.Make, connection.Model);
                    dbCar = CreateSmartCarCar(connection.SmartCarVehicleId, connection.Vin, connection.Make, connection.Model, dbCars);
                    _teslaSolarChargerContext.Cars.Add(dbCar);
                    dbCars.Add(dbCar);
                    changed = true;
                }

                if (dbCar.CarType != CarType.SmartCar)
                {
                    dbCar.CarType = CarType.SmartCar;
                    changed = true;
                }

                // Name a still-unnamed car from its make/model as soon as SmartCar reports them. This also
                // backfills the name of a placeholder that was created before the make/model were available.
                // A name the user (or a previous run) already set is never overwritten, and the VIN is never
                // used as the name.
                var smartCarName = BuildSmartCarName(connection.Make, connection.Model);
                if (!string.IsNullOrEmpty(smartCarName)
                    && (string.IsNullOrEmpty(dbCar.Name) || dbCar.Name == PendingCarName))
                {
                    dbCar.Name = smartCarName;
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
                        // Only the VIN is backfilled here. The name is intentionally NOT set to the VIN: a car is
                        // named from its make/model (see above), and an existing name is kept until the user
                        // changes it.
                        dbCar.Vin = connection.Vin;
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

    /// <summary>
    /// Returns true while the placeholder for <paramref name="smartCarVehicleId"/> is still inside its
    /// fast-refresh window. The first time a placeholder is seen its timestamp is stored in the (singleton)
    /// memory cache; from then on the window is measured against that fixed first-seen time. A sliding
    /// expiration keeps the entry alive only while the placeholder still exists and is polled each cycle, so it
    /// is cleaned up automatically once the VIN backfills or the car is demoted - and a permanently VIN-less
    /// placeholder stops forcing uncached refreshes once the window has elapsed.
    /// </summary>
    private bool IsPlaceholderWithinFastRefreshWindow(string smartCarVehicleId)
    {
        var cacheKey = PlaceholderFirstSeenCacheKeyPrefix + smartCarVehicleId;
        var firstSeen = _memoryCache.GetOrCreate(cacheKey, entry =>
        {
            // Keep alive well beyond the poll interval so reading it each cycle prevents eviction (and re-arming)
            // while the placeholder exists, but let it expire once we stop checking it.
            entry.SlidingExpiration = PlaceholderFastRefreshWindow + TimeSpan.FromMinutes(15);
            return _dateTimeProvider.DateTimeOffSetUtcNow();
        });
        return _dateTimeProvider.DateTimeOffSetUtcNow() - firstSeen < PlaceholderFastRefreshWindow;
    }

    // Builds a display name from the SmartCar make/model (e.g. "Tesla Model S"). Returns null when neither is
    // known, so callers can fall back to the placeholder name.
    private static string? BuildSmartCarName(string? make, string? model)
    {
        var name = $"{make} {model}".Trim();
        return string.IsNullOrEmpty(name) ? null : name;
    }

    private static Car CreateSmartCarCar(string smartCarVehicleId, string? vin, string? make, string? model, List<Car> existingCars)
    {
        var highestChargingPriority = existingCars.Any() ? existingCars.Max(c => c.ChargingPriority) : 0;
        return new Car
        {
            SmartCarVehicleId = smartCarVehicleId,
            Vin = string.IsNullOrEmpty(vin) ? null : vin,
            // Prefer the make/model name; fall back to the placeholder until SmartCar reports them (the name is
            // then backfilled on a later sync). The VIN is never used as the name.
            Name = BuildSmartCarName(make, model) ?? PendingCarName,
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
