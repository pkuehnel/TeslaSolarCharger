using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
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

    public async Task<Dictionary<string, string>> GetAllCurrentStatesAsync()
    {
        var states = new Dictionary<string, string>();

        try
        {
            // Get PV Values
            var pvValuesJson = await GetPvValuesJsonAsync();
            if (!string.IsNullOrEmpty(pvValuesJson))
            {
                states[DataTypeConstants.PvValues] = pvValuesJson;
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

    public async Task<string> GetCurrentStateAsync(string dataType)
    {
        try
        {
            // Check if it's a composite key (e.g., "CarState:1")
            var parts = dataType.Split(':');
            var baseDataType = parts[0];
            var entityId = parts.Length > 1 ? parts[1] : string.Empty;

            return baseDataType switch
            {
                DataTypeConstants.PvValues => await GetPvValuesJsonAsync(),
                DataTypeConstants.LoadPointOverviewValues => GetLoadPointOverviewValueJson(entityId),
                DataTypeConstants.CarOverviewState => GetCarOverviewValueJson(entityId),
                _ => string.Empty,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current state for {DataType}", dataType);
            return string.Empty;
        }
    }

    private async Task<Dictionary<string, string>> GetLoadPointOverviewValuesAsync()
    {
        var result = new Dictionary<string, string>();
        try
        {
            var matches = await _loadPointManagementService.GetCombinationsToManage().ConfigureAwait(false);
            foreach (var match in matches)
            {
                var loadPoint = _loadPointManagementService.GetLoadPointWithChargingValues(match);
                var entityKey = _entityKeyGenerationHelper.GetLoadPointEntityKey(loadPoint.CarId, loadPoint.ChargingConnectorId);
                var key = $"{DataTypeConstants.LoadPointOverviewValues}:{entityKey}";
                result[key] = JsonSerializer.Serialize(loadPoint);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting load point overview values");
        }
        return result;
    }

    private string GetLoadPointOverviewValueJson(string entityId)
    {
        try
        {
            var match = _entityKeyGenerationHelper.GetCombinationByKey(entityId);
            var loadPoint = _loadPointManagementService.GetLoadPointWithChargingValues(match);
            return JsonSerializer.Serialize(loadPoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting load point overview value for entity {EntityId}", entityId);
        }
        return string.Empty;
    }

    private string GetCarOverviewValueJson(string entityId)
    {
        try
        {
            if (int.TryParse(entityId, out var parsedCarId))
            {
                var loadPoint = _loadPointManagementService.GetCarOverviewState(parsedCarId);
                return JsonSerializer.Serialize(loadPoint);
            }
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting load point overview value for entity {EntityId}", entityId);
        }
        return string.Empty;
    }

    private async Task<string> GetPvValuesJsonAsync()
    {
        try
        {
            var pvValues = await _indexService.GetPvValues();
            return JsonSerializer.Serialize(pvValues);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serializing PV values");
            return string.Empty;
        }
    }
}
