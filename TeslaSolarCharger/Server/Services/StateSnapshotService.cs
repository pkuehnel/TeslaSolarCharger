using System.Text.Json;
using System.Text.Json.Serialization;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.SignalRClients;

namespace TeslaSolarCharger.Server.Services;

public class StateSnapshotService : IStateSnapshotService
{
    private readonly ILogger<StateSnapshotService> _logger;
    private readonly IIndexService _indexService;
    private readonly ILoadPointManagementService _loadPointManagementService;

    public StateSnapshotService(ILogger<StateSnapshotService> logger,
        IIndexService indexService,
        ILoadPointManagementService loadPointManagementService)
    {
        _logger = logger;
        _indexService = indexService;
        _loadPointManagementService = loadPointManagementService;
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
                _ => string.Empty,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current state for {DataType}", dataType);
            return string.Empty;
        }
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
