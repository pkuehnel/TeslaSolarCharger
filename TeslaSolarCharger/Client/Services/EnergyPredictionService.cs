using System.Globalization;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;

namespace TeslaSolarCharger.Client.Services;

public class EnergyPredictionService(ILogger<EnergyPredictionService> logger, IHttpClientHelper httpClientHelper) : IEnergyPredictionService
{
    public async Task<Dictionary<int, int>> GetPredictedSolarProductionByLocalHour(DateOnly date)
    {
        logger.LogTrace("{method}({date})", nameof(GetPredictedSolarProductionByLocalHour), date);
        var response = await httpClientHelper.SendGetRequestWithSnackbarAsync<Dictionary<int, int>>($"api/EnergyPrediction/GetSolarPrediction?date={date.ToString(CultureInfo.InvariantCulture)}");
        return response ?? new Dictionary<int, int>();
    }

    public async Task<Dictionary<int, int>> GetPredictedHouseConsumptionByLocalHour(DateOnly date)
    {
        logger.LogTrace("{method}({date})", nameof(GetPredictedHouseConsumptionByLocalHour), date);
        var response = await httpClientHelper.SendGetRequestWithSnackbarAsync<Dictionary<int, int>>($"api/EnergyPrediction/GetHouseConsumptionPrediction?date={date.ToString(CultureInfo.InvariantCulture)}");
        return response ?? new Dictionary<int, int>();
    }
}
