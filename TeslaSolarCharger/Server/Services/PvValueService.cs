using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class PvValueService : IPvValueService
{
    private readonly ILogger<PvValueService> _logger;
    private readonly ISettings _settings;
    private readonly IGridService _gridService;
    private readonly IInMemoryValues _inMemoryValues;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly ITelegramService _telegramService;

    public PvValueService(ILogger<PvValueService> logger, ISettings settings, IGridService gridService,
        IInMemoryValues inMemoryValues, IConfigurationWrapper configurationWrapper, ITelegramService telegramService)
    {
        _logger = logger;
        _settings = settings;
        _gridService = gridService;
        _inMemoryValues = inMemoryValues;
        _configurationWrapper = configurationWrapper;
        _telegramService = telegramService;
    }

    public async Task UpdatePvValues()
    {
        //ToDo: remove copied code
        _logger.LogTrace("{method}()", nameof(UpdatePvValues));

        var gridRequestUrl = _configurationWrapper.CurrentPowerToGridUrl();
        var gridRequestHeaders = _configurationWrapper.CurrentPowerToGridHeaders();
        var httpResponse = await GetHttpResponse(gridRequestUrl, gridRequestHeaders).ConfigureAwait(false);
        int? overage;
        if (!httpResponse.IsSuccessStatusCode)
        {
            overage = null;
            _logger.LogError("Could not get current overage. {statusCode}, {reasonPhrase}", httpResponse.StatusCode,
                httpResponse.ReasonPhrase);
            await _telegramService.SendMessage(
                $"Getting current grid power did result in statuscode {httpResponse.StatusCode} with reason {httpResponse.ReasonPhrase}").ConfigureAwait(false);
        }
        else
        {
            overage = await _gridService.GetCurrentOverage(httpResponse).ConfigureAwait(false);
        }

        _logger.LogDebug("Overage is {overage}", overage);
        _settings.Overage = overage;
        if (overage != null)
        {
            AddOverageValueToInMemoryList((int)overage);
        }

        var inverterRequestUrl = _configurationWrapper.CurrentInverterPowerUrl();
        var inverterRequestHeaders = _configurationWrapper.CurrentInverterPowerHeaders();

        var areInverterAndGridRequestUrlSame = string.Equals(gridRequestUrl, inverterRequestUrl,
            StringComparison.InvariantCultureIgnoreCase);
        _logger.LogTrace("inverter and grid request urls same: {value}", areInverterAndGridRequestUrlSame);

        var areInverterAndGridHeadersSame = gridRequestHeaders.Count == inverterRequestHeaders.Count
                && !gridRequestHeaders.Except(inverterRequestHeaders).Any();
        _logger.LogTrace("inverter and grid headers same: {value}", areInverterAndGridHeadersSame);

        if (!string.IsNullOrEmpty(inverterRequestUrl)
            && (!areInverterAndGridRequestUrlSame
                || !areInverterAndGridHeadersSame))
        {
            _logger.LogTrace("Send another request for inverter power");
            httpResponse = await GetHttpResponse(inverterRequestUrl, inverterRequestHeaders).ConfigureAwait(false);
        }

        if (!httpResponse.IsSuccessStatusCode || string.IsNullOrEmpty(inverterRequestUrl))
        {
            _settings.InverterPower = null;
            if (!string.IsNullOrEmpty(inverterRequestUrl))
            {
                _logger.LogError("Could not get current inverter power. {statusCode}, {reasonPhrase}", httpResponse.StatusCode,
                    httpResponse.ReasonPhrase);
                await _telegramService.SendMessage(
                    $"Getting current inverter power did result in statuscode {httpResponse.StatusCode} with reason {httpResponse.ReasonPhrase}").ConfigureAwait(false);
            }
        }
        else
        {
            _settings.InverterPower = await _gridService.GetCurrentInverterPower(httpResponse).ConfigureAwait(false);
        }

        var homeBatterySocRequestUrl = _configurationWrapper.HomeBatterySocUrl();
        var homeBatterySocHeaders = _configurationWrapper.HomeBatterySocHeaders();

        var areGridAndHomeBatterySocRequestUrlSame = string.Equals(inverterRequestUrl, homeBatterySocRequestUrl,
            StringComparison.InvariantCultureIgnoreCase);
        _logger.LogTrace("Home battery soc and inverter request urls same: {value}", areGridAndHomeBatterySocRequestUrlSame);

        var areinverterAndHomeBatterySocHeadersSame = inverterRequestHeaders.Count == homeBatterySocHeaders.Count
                                            && !inverterRequestHeaders.Except(homeBatterySocHeaders).Any();
        _logger.LogTrace("Home battery soc and inverter headers same: {value}", areinverterAndHomeBatterySocHeadersSame);

        if (!string.IsNullOrEmpty(homeBatterySocRequestUrl)
                                  && (!areGridAndHomeBatterySocRequestUrlSame
                                      || !areinverterAndHomeBatterySocHeadersSame))
        {
            _logger.LogTrace("Send another request for home battery soc");
            httpResponse = await GetHttpResponse(homeBatterySocRequestUrl, homeBatterySocHeaders).ConfigureAwait(false);
        }

        if (!httpResponse.IsSuccessStatusCode || string.IsNullOrEmpty(homeBatterySocRequestUrl))
        {
            _settings.HomeBatterySoc = null;
            if (!string.IsNullOrEmpty(homeBatterySocRequestUrl))
            {
                _logger.LogError("Could not get current home battery soc. {statusCode}, {reasonPhrase}", httpResponse.StatusCode,
                    httpResponse.ReasonPhrase);
                await _telegramService.SendMessage(
                    $"Getting current home battery soc did result in statuscode {httpResponse.StatusCode} with reason {httpResponse.ReasonPhrase}").ConfigureAwait(false);
            }
        }
        else
        {
            _settings.HomeBatterySoc = await _gridService.GetCurrentHomeBatterySoc(httpResponse).ConfigureAwait(false);
        }

        var homeBatteryPowerRequestUrl = _configurationWrapper.HomeBatteryPowerUrl();
        var homeBatteryPowerHeaders = _configurationWrapper.HomeBatteryPowerHeaders();

        var areHomeBatterySocAndHomeBatteryPowerRequestUrlSame = string.Equals(homeBatterySocRequestUrl, homeBatteryPowerRequestUrl,
            StringComparison.InvariantCultureIgnoreCase);
        _logger.LogTrace("Home battery power and home battery soc request urls same: {value}", areHomeBatterySocAndHomeBatteryPowerRequestUrlSame);

        var areHomeBatteryPowerAndGridHeadersSame = homeBatterySocHeaders.Count == homeBatteryPowerHeaders.Count
                                                    && !homeBatterySocHeaders.Except(homeBatteryPowerHeaders).Any();
        _logger.LogTrace("Home battery power and home battery soc headers same: {value}", areHomeBatteryPowerAndGridHeadersSame);

        if (!string.IsNullOrEmpty(homeBatteryPowerRequestUrl)
                                  && (!areHomeBatterySocAndHomeBatteryPowerRequestUrlSame
                                      || !areHomeBatteryPowerAndGridHeadersSame))
        {
            _logger.LogTrace("Send another request for home battery power");
            httpResponse = await GetHttpResponse(homeBatteryPowerRequestUrl, homeBatteryPowerHeaders).ConfigureAwait(false);
        }

        if (!httpResponse.IsSuccessStatusCode || string.IsNullOrEmpty(homeBatteryPowerRequestUrl))
        {
            _settings.HomeBatteryPower = null;
            if (!string.IsNullOrEmpty(homeBatteryPowerRequestUrl))
            {
                _logger.LogError("Could not get current home battery power. {statusCode}, {reasonPhrase}", httpResponse.StatusCode,
                    httpResponse.ReasonPhrase);
                await _telegramService.SendMessage(
                    $"Getting current home battery power did result in statuscode {httpResponse.StatusCode} with reason {httpResponse.ReasonPhrase}").ConfigureAwait(false);
            }
        }
        else
        {
            _settings.HomeBatteryPower = await _gridService.GetCurrentHomeBatteryPower(httpResponse).ConfigureAwait(false);
        }


    }

    private async Task<HttpResponseMessage> GetHttpResponse(string? gridRequestUrl, Dictionary<string, string> requestHeaders)
    {
        _logger.LogTrace("{method}({url}, {headers})", nameof(GetHttpResponse), gridRequestUrl, requestHeaders);
        if (string.IsNullOrEmpty(gridRequestUrl))
        {
            throw new ArgumentNullException(nameof(gridRequestUrl));
        }

        var request = new HttpRequestMessage(HttpMethod.Get, gridRequestUrl);
        request.Headers.Add("Accept", "*/*");
        foreach (var requestHeader in requestHeaders)
        {
            request.Headers.Add(requestHeader.Key, requestHeader.Value);
        }
        using var httpClient = new HttpClient();
        var response = await httpClient.SendAsync(request).ConfigureAwait(false);
        return response;
    }

    public int GetAveragedOverage()
    {
        _logger.LogTrace("{method}()", nameof(GetAveragedOverage));
        long weightedSum = 0;
        _logger.LogDebug("Build weighted average of {count} values", _inMemoryValues.OverageValues.Count);
        for (var i = 0; i < _inMemoryValues.OverageValues.Count; i++)
        {
            _logger.LogTrace("Power Value: {value}", _inMemoryValues.OverageValues[i]);
            weightedSum += _inMemoryValues.OverageValues[i] * (i + 1);
            _logger.LogTrace("weightedSum: {value}", weightedSum);
        }
        var weightedCount = _inMemoryValues.OverageValues.Count * (_inMemoryValues.OverageValues.Count + 1) / 2;
        if (weightedCount == 0)
        {
            throw new InvalidOperationException("There are no power values available");
        }
        return (int)(weightedSum / weightedCount);
    }

    private void AddOverageValueToInMemoryList(int overage)
    {
        _logger.LogTrace("{method}({overage})", nameof(AddOverageValueToInMemoryList), overage);
        _inMemoryValues.OverageValues.Add(overage);

        var valuesToSave = (int)(_configurationWrapper.ChargingValueJobUpdateIntervall().TotalSeconds /
                            _configurationWrapper.PvValueJobUpdateIntervall().TotalSeconds);

        if (_inMemoryValues.OverageValues.Count > valuesToSave)
        {
            _inMemoryValues.OverageValues.RemoveRange(0, _inMemoryValues.OverageValues.Count - valuesToSave);
        }
    }

    internal bool IsSameRequest(HttpRequestMessage httpRequestMessage1, HttpRequestMessage httpRequestMessage2)
    {
        if (httpRequestMessage1.Method != httpRequestMessage2.Method)
        {
            return false;
        }

        if (httpRequestMessage1.RequestUri != httpRequestMessage2.RequestUri)
        {
            return false;
        }

        if (httpRequestMessage1.Headers.Count() != httpRequestMessage2.Headers.Count())
        {
            return false;
        }

        foreach (var httpRequestHeader in httpRequestMessage1.Headers)
        {
            var message2Header = httpRequestMessage2.Headers.FirstOrDefault(h => h.Key.Equals(httpRequestHeader.Key));
            if (message2Header.Key == default)
            {
                return false;
            }
            var message2HeaderValue = message2Header.Value.ToList();
            foreach (var headerValue in httpRequestHeader.Value)
            {
                if (!message2HeaderValue.Any(v => string.Equals(v, headerValue, StringComparison.InvariantCulture)))
                {
                    return false;
                }
            }
        }


        return true;
    }
}
