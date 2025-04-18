﻿using System.Globalization;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TeslaSolarCharger.Client.Services;

public class EnergyDataService(ILogger<EnergyDataService> logger, IHttpClientHelper httpClientHelper) : IEnergyDataService
{
    public async Task<Dictionary<int, int>> GetPredictedSolarProductionByLocalHour(DateOnly date, CancellationToken token)
    {
        logger.LogTrace("{method}({date})", nameof(GetPredictedSolarProductionByLocalHour), date);
        var response = await httpClientHelper.SendGetRequestWithSnackbarAsync<Dictionary<int, int>>($"api/EnergyData/GetSolarPrediction?date={date.ToString("yyyy-MM-dd")}", token);
        token.ThrowIfCancellationRequested();
        return response ?? new Dictionary<int, int>();
    }

    public async Task<Dictionary<int, int>> GetPredictedHouseConsumptionByLocalHour(DateOnly date, CancellationToken token)
    {
        logger.LogTrace("{method}({date})", nameof(GetPredictedHouseConsumptionByLocalHour), date);
        var response = await httpClientHelper.SendGetRequestWithSnackbarAsync<Dictionary<int, int>>($"api/EnergyData/GetHouseConsumptionPrediction?date={date.ToString("yyyy-MM-dd")}", token);
        token.ThrowIfCancellationRequested();
        return response ?? new Dictionary<int, int>();
    }

    public async Task<Dictionary<int, int>> GetActualSolarProductionByLocalHour(DateOnly date, CancellationToken token)
    {
        logger.LogTrace("{method}({date})", nameof(GetActualSolarProductionByLocalHour), date);
        var response = await httpClientHelper.SendGetRequestWithSnackbarAsync<Dictionary<int, int>>($"api/EnergyData/GetSolarActual?date={date.ToString("yyyy-MM-dd")}", token);
        token.ThrowIfCancellationRequested();
        return response ?? new Dictionary<int, int>();
    }

    public async Task<Dictionary<int, int>> GetActualHouseConsumptionByLocalHour(DateOnly date, CancellationToken token)
    {
        logger.LogTrace("{method}({date})", nameof(GetActualHouseConsumptionByLocalHour), date);
        var response = await httpClientHelper.SendGetRequestWithSnackbarAsync<Dictionary<int, int>>($"api/EnergyData/GetHouseActual?date={date.ToString("yyyy-MM-dd")}", token);
        token.ThrowIfCancellationRequested();
        return response ?? new Dictionary<int, int>();
    }

    public async Task<bool> SolarPowerPredictionEnabled()
    {
        logger.LogTrace("{method}()", nameof(SolarPowerPredictionEnabled));
        var response = await httpClientHelper.SendGetRequestWithSnackbarAsync<DtoValue<bool>>("api/BaseConfiguration/IsPredictSolarPowerGenerationEnabled");
        if (response == default)
        {
            return false;
        }
        return response.Value;
    }

    public async Task<bool> ShowEnergyDataOnHome()
    {
        logger.LogTrace("{method}()", nameof(ShowEnergyDataOnHome));
        var response = await httpClientHelper.SendGetRequestWithSnackbarAsync<DtoValue<bool>>("api/BaseConfiguration/ShowEnergyDataOnHome");
        if (response == default)
        {
            return false;
        }
        return response.Value;
    }
}
