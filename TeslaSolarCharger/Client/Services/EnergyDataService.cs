using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Client.Services
{
    public class EnergyDataService : IEnergyDataService
    {
        private readonly ILogger<EnergyDataService> _logger;
        private readonly IHttpClientHelper _http;

        public EnergyDataService(
            ILogger<EnergyDataService> logger,
            IHttpClientHelper httpClientHelper)
        {
            _logger = logger;
            _http = httpClientHelper;
        }

        public Task<Dictionary<int, int>> GetPredictedSolarProductionByLocalHour(
            DateOnly date, CancellationToken token) =>
            GetHourlyDictionary(date, "GetSolarPrediction", token);

        public Task<Dictionary<int, int>> GetPredictedHouseConsumptionByLocalHour(
            DateOnly date, CancellationToken token) =>
            GetHourlyDictionary(date, "GetHouseConsumptionPrediction", token);

        public Task<Dictionary<int, int>> GetActualSolarProductionByLocalHour(
            DateOnly date, CancellationToken token) =>
            GetHourlyDictionary(date, "GetSolarActual", token);

        public Task<Dictionary<int, int>> GetActualHouseConsumptionByLocalHour(
            DateOnly date, CancellationToken token) =>
            GetHourlyDictionary(date, "GetHouseActual", token);

        public Task<bool> SolarPowerPredictionEnabled() =>
            GetBooleanFlag("IsPredictSolarPowerGenerationEnabled");

        public Task<bool> ShowEnergyDataOnHome() =>
            GetBooleanFlag("ShowEnergyDataOnHome");

        // ————————————————————————————————————————————————————————————————
        // Generic helper for all hourly‐series endpoints
        private async Task<Dictionary<int, int>> GetHourlyDictionary(
            DateOnly date,
            string endpointName,
            CancellationToken token)
        {
            var startDate = ToUtcOffset(date);
            var endDate = ToUtcOffset(date.AddDays(1));
            var sliceLength = TimeSpan.FromHours(1);

            // build ISO-8601 query parameters
            var qs = new Dictionary<string, string>
            {
                ["startDate"] = startDate.ToString("O", CultureInfo.InvariantCulture),
                ["endDate"] = endDate.ToString("O", CultureInfo.InvariantCulture),
                ["sliceLength"] = sliceLength.ToString("g"),
            };
            var url = QueryHelpers.AddQueryString(
                $"api/EnergyData/{endpointName}", qs);

            var resp = await _http
                .SendGetRequestWithSnackbarAsync<Dictionary<DateTimeOffset, int>>(url, token);

            token.ThrowIfCancellationRequested();
            if (resp == default) return new();

            // map UTC-hour → value
            return resp.ToDictionary(x => x.Key.ToLocalTime().Hour, x => x.Value);
        }

        // Generic helper for simple bool‐flag endpoints
        private async Task<bool> GetBooleanFlag(string configName)
        {
            var resp = await _http
                .SendGetRequestWithSnackbarAsync<DtoValue<bool>>(
                    $"api/BaseConfiguration/{configName}");

            return resp?.Value ?? false;
        }

        // Converts local midnight of the given date to a zero-offset DateTimeOffset
        private static DateTimeOffset ToUtcOffset(DateOnly date)
        {
            var localMidnight = date.ToDateTime(new TimeOnly(0, 0));
            return new DateTimeOffset(localMidnight.ToUniversalTime(), TimeSpan.Zero);
        }
    }
}
