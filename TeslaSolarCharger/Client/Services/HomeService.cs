using MudBlazor;
using System.ComponentModel;
using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Client.Services;

public class HomeService : IHomeService
{
    private readonly ILogger<HomeService> _logger;
    private readonly IHttpClientHelper _httpClientHelper;
    private readonly ISnackbar _snackbar;

    public HomeService(ILogger<HomeService> logger,
        IHttpClientHelper httpClientHelper,
        ISnackbar snackbar)
    {
        _logger = logger;
        _httpClientHelper = httpClientHelper;
        _snackbar = snackbar;
    }

    public async Task<List<DtoLoadPointOverview>?> GetLoadPointsToManage()
    {
        _logger.LogTrace("{method}()", nameof(GetLoadPointsToManage));
        var result = await _httpClientHelper.SendGetRequestAsync<List<DtoLoadPointOverview>>("api/Home/GetLoadPointsToManage");
        if (result.HasError)
        {
            _logger.LogError(result.ErrorMessage);
            _snackbar.Add("Error while getting Loadpoint overviews", Severity.Error);
            return null;
        }
        return result.Data;
    }

    public async Task<List<DtoChargingSchedule>?> GetChargingSchedules(int? carId, int? chargingConnectorId)
    {
        _logger.LogTrace("{method}()", nameof(GetChargingSchedules));
        var result = await _httpClientHelper.SendGetRequestAsync<List<DtoChargingSchedule>>($"api/Home/GetChargingSchedules?carId={carId}&chargingConnectorId={chargingConnectorId}");
        if (result.HasError)
        {
            _logger.LogError(result.ErrorMessage);
            _snackbar.Add("Error while getting Charging schedules", Severity.Error);
            return null;
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

    public async Task<DtoCarOverview?> GetCarOverview(int carId)
    {
        _logger.LogTrace("{method}()", nameof(GetCarChargingTargets));
        var result = await _httpClientHelper.SendGetRequestAsync<DtoCarOverview>($"api/Home/GetCarOverview?carId={carId}");
        if (result.HasError)
        {
            _logger.LogError(result.ErrorMessage);
        }
        return result.Data;
    }

    public async Task<DtoChargingConnectorOverview?> GetChargingConnectorOverview(int chargingConnectorId)
    {
        _logger.LogTrace("{method}()", nameof(GetCarChargingTargets));
        var result = await _httpClientHelper.SendGetRequestAsync<DtoChargingConnectorOverview>($"api/Home/GetChargingConnectorOverview?chargingConnectorId={chargingConnectorId}");
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
        _logger.LogTrace("{method}()", nameof(GetLoadPointsToManage));
        var result = await _httpClientHelper.SendGetRequestAsync<DtoChargeSummary>($"api/ChargingCost/GetChargeSummary?carId={carId}&chargingConnectorId={chargingConnectorId}");
        if (result.HasError)
        {
            _logger.LogError(result.ErrorMessage);
        }
        return result.Data ?? new();
    }
}
