using MudBlazor;
using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
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

    public async Task<Result<object>> UpdateChargingStationConnector(DtoChargingStationConnector chargingStationConnector)
    {
        _logger.LogTrace("{method}()", nameof(UpdateChargingStationConnector));
        // The full Result is returned so callers can display ValidationProblemDetails on the form fields.
        return await _httpClientHelper.SendPostRequestAsync<object>("api/ChargingStations/UpdateChargingStationConnector", chargingStationConnector);
    }

    public async Task DeleteChargingStation(int chargingStationId)
    {
        _logger.LogTrace("{method}({chargingStationId})", nameof(DeleteChargingStation), chargingStationId);
        await _httpClientHelper.SendDeleteRequestWithSnackbarAsync<object>($"api/ChargingStations/DeleteChargingStation?chargingStationId={chargingStationId}");
    }

    public async Task<DtoChargingStationDeletionProgress?> GetChargingStationDeletionProgress(int chargingStationId)
    {
        _logger.LogTrace("{method}({chargingStationId})", nameof(GetChargingStationDeletionProgress), chargingStationId);
        // Use the non-snackbar variant: while no deletion runs the server returns null, which the helper reports
        // as an error. As this is polled every second, only log it instead of spamming error snackbars.
        var result = await _httpClientHelper.SendGetRequestAsync<DtoChargingStationDeletionProgress?>($"api/ChargingStations/GetChargingStationDeletionProgress?chargingStationId={chargingStationId}");
        if (result.HasError)
        {
            _logger.LogTrace("Could not get charging station deletion progress: {errorMessage}", result.ErrorMessage);
        }
        return result.Data;
    }

    public async Task<Dictionary<int, string>?> GetCarOptions()
    {
        _logger.LogTrace("{method}()", nameof(GetCarOptions));
        var response = await _httpClientHelper.SendGetRequestWithSnackbarAsync<Dictionary<int, string>>($"api/ChargingStations/GetCarOptions");
        return response;
    }
}
