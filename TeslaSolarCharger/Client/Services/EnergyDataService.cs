using System.Globalization;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TeslaSolarCharger.Client.Services;

public class EnergyDataService(ILogger<EnergyDataService> logger, IHttpClientHelper httpClientHelper) : IEnergyDataService
{
    public async Task<Dictionary<int, int>> GetPredictedSolarProductionByLocalHour(DateOnly date)
    {
        logger.LogTrace("{method}({date})", nameof(GetPredictedSolarProductionByLocalHour), date);
        var response = await httpClientHelper.SendGetRequestWithSnackbarAsync<Dictionary<int, int>>($"api/EnergyData/GetSolarPrediction?date={date.ToString(CultureInfo.InvariantCulture)}");
        return response ?? new Dictionary<int, int>();
    }

    public async Task<Dictionary<int, int>> GetPredictedHouseConsumptionByLocalHour(DateOnly date)
    {
        logger.LogTrace("{method}({date})", nameof(GetPredictedHouseConsumptionByLocalHour), date);
        var response = await httpClientHelper.SendGetRequestWithSnackbarAsync<Dictionary<int, int>>($"api/EnergyData/GetHouseConsumptionPrediction?date={date.ToString(CultureInfo.InvariantCulture)}");
        return response ?? new Dictionary<int, int>();
    }

    public async Task<Dictionary<int, int>> GetActualSolarProductionByLocalHour(DateOnly date)
    {
        logger.LogTrace("{method}({date})", nameof(GetActualSolarProductionByLocalHour), date);
        var response = await httpClientHelper.SendGetRequestWithSnackbarAsync<Dictionary<int, int>>($"api/EnergyData/GetSolarActual?date={date.ToString(CultureInfo.InvariantCulture)}");
        return response ?? new Dictionary<int, int>();
    }

    public async Task<Dictionary<int, int>> GetActualHouseConsumptionByLocalHour(DateOnly date)
    {
        logger.LogTrace("{method}({date})", nameof(GetActualHouseConsumptionByLocalHour), date);
        var response = await httpClientHelper.SendGetRequestWithSnackbarAsync<Dictionary<int, int>>($"api/EnergyData/GetHouseActual?date={date.ToString(CultureInfo.InvariantCulture)}");
        return response ?? new Dictionary<int, int>();
    }

    public async Task<bool> SolarPowerPredictionEnabled()
    {
        logger.LogTrace("{method}()", nameof(SolarPowerPredictionEnabled));
        var response = await httpClientHelper.SendGetRequestWithSnackbarAsync<DtoValue<bool>>("api/Hello/IsPredictSolarValuesEnabled");
        if (response == default)
        {
            return false;
        }
        return response.Value;
    }
}
