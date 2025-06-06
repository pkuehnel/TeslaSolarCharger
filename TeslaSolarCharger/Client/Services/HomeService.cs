using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Client.Services;

public class HomeService : IHomeService
{
    private readonly ILogger<HomeService> _logger;
    private readonly IHttpClientHelper _httpClientHelper;

    public HomeService(ILogger<HomeService> logger,
        IHttpClientHelper httpClientHelper)
    {
        _logger = logger;
        _httpClientHelper = httpClientHelper;
    }

    public async Task<List<DtoLoadPointOverview>?> GetPluggedInLoadPoints()
    {
        _logger.LogTrace("{method}()", nameof(GetPluggedInLoadPoints));
        var result = await _httpClientHelper.SendGetRequestAsync<List<DtoLoadPointOverview>>("api/Home/GetLoadPointOverviews");
        if (result.HasError)
        {
            _logger.LogError(result.ErrorMessage);
        }
        return result.Data;
    }

    public async Task<List<DtoCarChargingTarget>?> GetCarChargingTargets(int carId)
    {
        _logger.LogTrace("{method}()", nameof(GetCarChargingTargets));
        var result = await _httpClientHelper.SendGetRequestAsync<List<DtoCarChargingTarget>>($"api/Home/GetCarChargingTargets?carId={carId}");
        if (result.HasError)
        {
            _logger.LogError(result.ErrorMessage);
        }
        return result.Data;
    }

    public async Task UpdateCarMinSoc(int carId, int minSoc)
    {
        _logger.LogTrace("{method}()", nameof(UpdateCarMinSoc));
        await _httpClientHelper.SendPostRequestWithSnackbarAsync<object>($"api/Home/UpdateCarMinSoc?carId={carId}&minSoc={minSoc}", null);
    }

    public async Task<Result<object>> DeleteCarChargingTarget(int chargingTargetId)
    {
        _logger.LogTrace("{method}()", nameof(DeleteCarChargingTarget));
        return await _httpClientHelper.SendDeleteRequestAsync($"api/Home/DeleteCarChargingTarget?chargingTargetId={chargingTargetId}");
    }

    public async Task<DtoChargeSummary> GetChargeSummary(int? carId, int? chargingConnectorId)
    {
        _logger.LogTrace("{method}()", nameof(GetPluggedInLoadPoints));
        var result = await _httpClientHelper.SendGetRequestAsync<DtoChargeSummary>($"api/ChargingCost/GetChargeSummary?carId={carId}&chargingConnectorId={chargingConnectorId}");
        if (result.HasError)
        {
            _logger.LogError(result.ErrorMessage);
        }
        return result.Data ?? new();
    }
}
