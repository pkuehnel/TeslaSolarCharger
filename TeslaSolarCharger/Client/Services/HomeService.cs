using MudBlazor;
using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Enums;

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

    public async Task<Result<object?>> UpdateCarMinSoc(int carId, int minSoc)
    {
        _logger.LogTrace("{method}()", nameof(UpdateCarMinSoc));
        var result = await _httpClientHelper.SendPostRequestAsync<object?>($"api/Home/UpdateCarMinSoc?carId={carId}&minSoc={minSoc}", null);
        return result;
    }

    public async Task<Result<object?>> UpdateCarMaxSoc(int carId, int soc)
    {
        _logger.LogTrace("{method}()", nameof(UpdateCarMaxSoc));
        var result = await _httpClientHelper.SendPostRequestAsync<object?>($"api/Home/UpdateCarMaxSoc?carId={carId}&soc={soc}", null);
        return result;
    }

    public async Task<Result<object?>> UpdateCarChargeMode(int carId, ChargeModeV2 chargeMode)
    {
        _logger.LogTrace("{method}()", nameof(UpdateCarChargeMode));
        var result = await _httpClientHelper.SendPostRequestAsync<object?>($"api/Home/UpdateCarChargeMode?carId={carId}&chargeMode={chargeMode}", null);
        return result;
    }

    public async Task<Result<object?>> UpdateChargingConnectorChargeMode(int chargingConnectorId, ChargeModeV2 chargeMode)
    {
        _logger.LogTrace("{method}()", nameof(UpdateChargingConnectorChargeMode));
        var result = await _httpClientHelper.SendPostRequestAsync<object?>($"api/Home/UpdateChargingConnectorChargeMode?chargingConnectorId={chargingConnectorId}&chargeMode={chargeMode}", null);
        return result;
    }

    public async Task<Result<object?>> StartChargingConnectorCharging(int chargingConnectorId, int currentToSet, int? numberOfPhases)
    {
        _logger.LogTrace("{method}({chargingConnectorId}, {currentToSet}, {numberOfPhases})", nameof(StartChargingConnectorCharging), chargingConnectorId, currentToSet, numberOfPhases);
        var result = await _httpClientHelper.SendPostRequestAsync<object?>($"api/Home/StartChargingConnectorCharging?chargingConnectorId={chargingConnectorId}&currentToSet={currentToSet}&numberOfPhases={numberOfPhases}", null);
        return result;
    }

    public async Task<Result<object?>> SetChargingConnectorCurrent(int chargingConnectorId, int currentToSet, int? numberOfPhases)
    {
        _logger.LogTrace("{method}({chargingConnectorId}, {currentToSet}, {numberOfPhases})", nameof(SetChargingConnectorCurrent), chargingConnectorId, currentToSet, numberOfPhases);
        var result = await _httpClientHelper.SendPostRequestAsync<object?>($"api/Home/SetChargingConnectorCurrent?chargingConnectorId={chargingConnectorId}&currentToSet={currentToSet}&numberOfPhases={numberOfPhases}", null);
        return result;
    }

    public async Task<Result<object?>> StopChargingConnectorCharging(int chargingConnectorId)
    {
        _logger.LogTrace("{method}({chargingConnectorId})", nameof(StopChargingConnectorCharging), chargingConnectorId);
        var result = await _httpClientHelper.SendPostRequestAsync<object?>($"api/Home/StopChargingConnectorCharging?chargingConnectorId={chargingConnectorId}", null);
        return result;
    }

    public async Task<Result<object?>> SetCarChargingCurrent(int carId, int currentToSet)
    {
        _logger.LogTrace("{method}({chargingConnectorId}, {currentToSet})", nameof(SetCarChargingCurrent), carId, currentToSet);
        var result = await _httpClientHelper.SendPostRequestAsync<object?>($"api/Home/SetCarChargingCurrent?carId={carId}&currentToSet={currentToSet}", null);
        return result;
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

    public async Task<List<DtoNotChargingWithExpectedPowerReason>?> GetNotChargingWithExpectedPowerReasons(int? carId, int? connectorId)
    {
        _logger.LogTrace("{method}({carId}, {connectorId})", nameof(GetNotChargingWithExpectedPowerReasons), carId, connectorId);
        var result = await _httpClientHelper.SendGetRequestAsync<List<DtoNotChargingWithExpectedPowerReason>>($"api/Home/GetNotChargingWithExpectedPowerReasons?carId={carId}&connectorId={connectorId}");
        if (result.HasError)
        {
            _logger.LogError(result.ErrorMessage);
            _snackbar.Add("Error while getting Not charging with expected power reasons", Severity.Error);
        }
        return result.Data;
    }

    public async Task<Dictionary<int, string>?> GetLoadPointCarOptions()
    {
        _logger.LogTrace("{method}()", nameof(GetLoadPointCarOptions));
        var result = await _httpClientHelper.SendGetRequestAsync<Dictionary<int, string>>($"api/Home/GetLoadPointCarOptions");
        if (result.HasError)
        {
            _logger.LogError(result.ErrorMessage);
            _snackbar.Add("Error while getting Not charging with expected power reasons", Severity.Error);
        }
        return result.Data;
    }

    public async Task<Result<object?>> UpdateCarForLoadpoint(int chargingConnectorId, int? carId)
    {
        _logger.LogTrace("{method}({chargingConnectorId})", nameof(StopChargingConnectorCharging), chargingConnectorId);
        var result = await _httpClientHelper.SendPostRequestAsync<object?>($"api/Home/UpdateCarForLoadpoint?chargingConnectorId={chargingConnectorId}&carId={carId}", null);
        return result;
    }
}
