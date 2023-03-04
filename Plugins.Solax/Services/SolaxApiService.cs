using Newtonsoft.Json;
using Plugins.Solax.Dtos;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.SharedBackend.Contracts;
using TeslaSolarCharger.SharedBackend.Dtos;

namespace Plugins.Solax.Services;

public class SolaxApiService : ICurrentValuesService
{
    private readonly ILogger<SolaxApiService> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IConfiguration _configuration;

    public SolaxApiService(ILogger<SolaxApiService> logger, IDateTimeProvider dateTimeProvider, IConfiguration configuration)
    {
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
        _configuration = configuration;
    }

    public async Task<DtoCurrentPvValues> GetCurrentPvValues()
    {
        _logger.LogTrace("{method}()", nameof(GetCurrentPvValues));
        var solarSystemBaseUrlName = "SolarSystemBaseUrl";
        var url = _configuration.GetValue<string>(solarSystemBaseUrlName);
        if (string.IsNullOrEmpty(url))
        {
            var exception = new InvalidOperationException($"{solarSystemBaseUrlName} id empty. Can not get solar values.");
            _logger.LogError(exception, "Getting Pv values is not possible.");
            throw exception;
        }
        var parameters = new List<KeyValuePair<string, string>>()
        {
            new("optType", "ReadRealTimeData"),
            new ("pwd", _configuration.GetValue<string>("SolarSystemPassword") ?? string.Empty ),
        };

        var httpResponse = await GetHttpResonse(url, parameters).ConfigureAwait(false);
        var serializedString = await httpResponse.Content.ReadAsStringAsync();
        var solaxDto = JsonConvert.DeserializeObject<DtoSolaxValues>(serializedString);

        if (solaxDto == null)
        {
            var exception = new InvalidDataException("Returned data can not be deserialized.");
            _logger.LogError(exception, "Returned string is {string}", serializedString);
            throw exception;
        }

        var currentPvValues = GetPvValuesFromSolaxDto(solaxDto, _dateTimeProvider.DateTimeOffSetNow());
        return currentPvValues;
    }

    private DtoCurrentPvValues GetPvValuesFromSolaxDto(DtoSolaxValues solaxDto, DateTimeOffset dateTimeOffSetNow)
    {
        var uncalculatedBatteryPower = solaxDto.Data[_configuration.GetValue<int>("BatteryPowerIndex")];
        var uncalculatedGridPower = solaxDto.Data[_configuration.GetValue<int>("GridPowerIndex")];
        var switchPoint = _configuration.GetValue<int>("SolarSystemSwitchPoint");
        var maxPoint = _configuration.GetValue<int>("SolarSystemMaxPoint");
        var actualBatteryPower = uncalculatedBatteryPower < switchPoint ? uncalculatedBatteryPower : maxPoint - uncalculatedBatteryPower;
        var actualGridPower = uncalculatedGridPower < switchPoint ? uncalculatedGridPower : maxPoint - uncalculatedGridPower;

        var pv1Power = solaxDto.Data[_configuration.GetValue<int>("PvPower1Index")];
        var pv2Power = solaxDto.Data[_configuration.GetValue<int>("PvPower2Index")];
        var currentPvValues = new DtoCurrentPvValues()
        {
            InverterPower = pv1Power + pv2Power,
            GridPower = actualGridPower,
            HomeBatteryPower = actualBatteryPower,
            HomeBatterySoc = solaxDto.Data[_configuration.GetValue<int>("BatterySocIndex")],
            LastUpdated = dateTimeOffSetNow,
        };
        return currentPvValues;
    }


    private async Task<HttpResponseMessage> GetHttpResonse(string url, List<KeyValuePair<string, string>> parameters)
    {
        _logger.LogTrace("{method}({param1}, {param2})", nameof(GetHttpResonse), url, parameters);
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        var content = new FormUrlEncodedContent(parameters);
        request.Content = content;
        using var httpClient = new HttpClient();
        var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var responseContentString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            _logger.LogError("Error while sending post to Solax. Response: {response}", responseContentString);
        }
        response.EnsureSuccessStatusCode();
        return response;
    }
}

