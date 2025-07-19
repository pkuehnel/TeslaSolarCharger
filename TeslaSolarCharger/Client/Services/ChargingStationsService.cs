using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.ChargingStation;

namespace TeslaSolarCharger.Client.Services;

public class ChargingStationsService : IChargingStationsService
{
    private readonly ILogger<ChargingStationsService> _logger;
    private readonly IHttpClientHelper _httpClientHelper;

    public ChargingStationsService(ILogger<ChargingStationsService> logger, IHttpClientHelper httpClientHelper)
    {
        _logger = logger;
        _httpClientHelper = httpClientHelper;
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
}
