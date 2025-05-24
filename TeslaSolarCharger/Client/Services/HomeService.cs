using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
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

    public async Task<List<DtoCarChargingSchedule>?> GetCarChargingSchedules(int carId)
    {
        _logger.LogTrace("{method}()", nameof(GetPluggedInLoadPoints));
        var result = await _httpClientHelper.SendGetRequestAsync<List<DtoCarChargingSchedule>>($"api/Home/GetCarChargingSchedules?carId={carId}");
        if (result.HasError)
        {
            _logger.LogError(result.ErrorMessage);
        }
        return result.Data;
    }

    public async Task UpdateCarMinSoc(int carId, int minSoc)
    {
        _logger.LogTrace("{method}()", nameof(GetPluggedInLoadPoints));
        await _httpClientHelper.SendPostRequestWithSnackbarAsync<object>($"api/Home/UpdateCarMinSoc?carId={carId}&minSoc={minSoc}", null);
    }

    public async Task<Result<Result<int>>> SaveCarChargingSchedule(int carId, DtoCarChargingSchedule dto)
    {
        _logger.LogTrace("{method}()", nameof(GetPluggedInLoadPoints));
        var result = await _httpClientHelper.SendPostRequestAsync<Result<int>>($"api/Home/SaveCarChargingSchedule?carId={carId}", dto);
        if (result.HasError)
        {
            _logger.LogError(result.ErrorMessage);
        }
        return result;
    }
}
