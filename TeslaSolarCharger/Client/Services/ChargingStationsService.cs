using MudBlazor;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.ChargingStation;

namespace TeslaSolarCharger.Client.Services;

public class ChargingStationsService : IChargingStationsService
{
    private readonly ILogger<ChargingStationsService> _logger;
    private readonly IHttpClientHelper _httpClientHelper;
    private readonly ISnackbar _snackbar;

    public ChargingStationsService(ILogger<ChargingStationsService> logger, IHttpClientHelper httpClientHelper, ISnackbar snackbar)
    {
        _logger = logger;
        _httpClientHelper = httpClientHelper;
        _snackbar = snackbar;
    }

    public async Task<List<DtoChargingStation>?> GetChargingStations()
    {
        _logger.LogTrace("{method}()", nameof(GetChargingStations));
        var response = await _httpClientHelper.SendGetRequestWithSnackbarAsync<List<DtoChargingStation>>("api/ChargingStations/GetChargingStations");
        return response;
    }

    public async Task<List<DtoChargingStationConnector>?> GetChargingStationConnectors(int chargingStationId)
    {
        _logger.LogTrace("{method}()", nameof(GetChargingStationConnectors));
        var response = await _httpClientHelper.SendGetRequestWithSnackbarAsync<List<DtoChargingStationConnector>>($"api/ChargingStations/GetChargingStationConnectors?chargingStationId={chargingStationId}");
        return response;
    }

    public async Task<bool> UpdateChargingStationConnector(DtoChargingStationConnector chargingStationConnector)
    {
        _logger.LogTrace("{method}()", nameof(UpdateChargingStationConnector));
        var result = await _httpClientHelper.SendPostRequestAsync<object>("api/ChargingStations/UpdateChargingStationConnector", chargingStationConnector);
        if (result.HasError)
        {
            _snackbar.Add(result.ErrorMessage ?? "EmptyErrorMessage", Severity.Error);
            return false;
        }
        return true;
    }

    public async Task DeleteChargingStation(int chargingStationId)
    {
        _logger.LogTrace("{method}({chargingStationId})", nameof(DeleteChargingStation), chargingStationId);
        await _httpClientHelper.SendDeleteRequestWithSnackbarAsync<object>($"api/ChargingStations/DeleteChargingStation?chargingStationId={chargingStationId}");
    }

    public async Task<Dictionary<int, string>?> GetCarOptions()
    {
        _logger.LogTrace("{method}()", nameof(GetCarOptions));
        var response = await _httpClientHelper.SendGetRequestWithSnackbarAsync<Dictionary<int, string>>($"api/ChargingStations/GetCarOptions");
        return response;
    }
}
