using System.Text.Json;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;
using TeslaSolarCharger.Shared.Helper.Contracts;
using TeslaSolarCharger.Shared.SignalRClients;

namespace TeslaSolarCharger.Server.Services;

public class StateSnapshotService : IStateSnapshotService
{
    private readonly ILogger<StateSnapshotService> _logger;
    private readonly IIndexService _indexService;
    private readonly ILoadPointManagementService _loadPointManagementService;
    private readonly IEntityKeyGenerationHelper _entityKeyGenerationHelper;

    public StateSnapshotService(ILogger<StateSnapshotService> logger,
        IIndexService indexService,
        ILoadPointManagementService loadPointManagementService,
        IEntityKeyGenerationHelper entityKeyGenerationHelper)
    {
        _logger = logger;
        _indexService = indexService;
        _loadPointManagementService = loadPointManagementService;
        _entityKeyGenerationHelper = entityKeyGenerationHelper;
    }

    public async Task<Dictionary<string, object?>> GetAllCurrentStatesAsync()
    {
        var states = new Dictionary<string, object?>();

        try
        {
            // Get PV Values
            var pvValues = await GetPvValuesAsync();
            if (pvValues != null)
            {
                states[DataTypeConstants.PvValues] = pvValues;
            }
            var loadPointStates = await GetLoadPointOverviewValuesAsync();
            foreach (var kvp in loadPointStates)
            {
                states[kvp.Key] = kvp.Value;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all current states");
        }

        return states;
    }

    public async Task<object?> GetCurrentStateAsync(string dataType, string entityId)
    {
        try
        {
            return dataType switch
            {
                DataTypeConstants.PvValues => await GetPvValuesAsync(),
                DataTypeConstants.LoadPointOverviewValues => GetLoadPointOverviewValue(entityId),
                DataTypeConstants.CarOverviewState => GetCarOverviewValue(entityId),
                DataTypeConstants.ChargingConnectorOverviewState => GetChargingConnectorOverviewValue(entityId),
                _ => null,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current state for {DataType}", dataType);
            return null;
        }
    }

    private async Task<Dictionary<string, DtoLoadPointWithCurrentChargingValues>> GetLoadPointOverviewValuesAsync()
    {
        var result = new Dictionary<string, DtoLoadPointWithCurrentChargingValues>();
        try
        {
            var matches = await _loadPointManagementService.GetCombinationsToManage().ConfigureAwait(false);
            foreach (var match in matches)
            {
                var loadPoint = _loadPointManagementService.GetLoadPointWithChargingValues(match);
                var entityKey = _entityKeyGenerationHelper.GetLoadPointEntityKey(loadPoint.CarId, loadPoint.ChargingConnectorId);
                var key = $"{DataTypeConstants.LoadPointOverviewValues}:{entityKey}";
                result[key] = loadPoint;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting load point overview values");
        }
        return result;
    }

    private DtoLoadPointWithCurrentChargingValues? GetLoadPointOverviewValue(string entityId)
    {
        try
        {
            var match = _entityKeyGenerationHelper.GetCombinationByKey(entityId);
            var loadPoint = _loadPointManagementService.GetLoadPointWithChargingValues(match);
            return loadPoint;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting load point overview value for entity {EntityId}", entityId);
        }
        return null;
    }

    private DtoCarOverviewState? GetCarOverviewValue(string entityId)
    {
        try
        {
            if (int.TryParse(entityId, out var parsedCarId))
            {
                var loadPoint = _loadPointManagementService.GetCarOverviewState(parsedCarId);
                return loadPoint;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting car overview value for entity {EntityId}", entityId);
        }
        return null;
    }

    private DtoChargingConnectorOverviewState? GetChargingConnectorOverviewValue(string entityId)
    {
        try
        {
            if (int.TryParse(entityId, out var parsedCarId))
            {
                var loadPoint = _loadPointManagementService.GetChargingConnectorOverviewState(parsedCarId);
                return loadPoint;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting charging connector overview value for entity {EntityId}", entityId);
        }
        return null;
    }

    private async Task<DtoPvValues?> GetPvValuesAsync()
    {
        try
        {
            var pvValues = await _indexService.GetPvValues();
            return pvValues;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serializing PV values");
            return null;
        }
    }
}
