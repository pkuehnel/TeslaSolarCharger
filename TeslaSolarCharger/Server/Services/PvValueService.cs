using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Globalization;
using System.Xml;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class PvValueService : IPvValueService
{
    private readonly ILogger<PvValueService> _logger;
    private readonly ISettings _settings;
    private readonly IInMemoryValues _inMemoryValues;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly ITelegramService _telegramService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PvValueService(ILogger<PvValueService> logger, ISettings settings,
        IInMemoryValues inMemoryValues, IConfigurationWrapper configurationWrapper,
        ITelegramService telegramService,IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _settings = settings;
        _inMemoryValues = inMemoryValues;
        _configurationWrapper = configurationWrapper;
        _telegramService = telegramService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task UpdatePvValues()
    {
        _logger.LogTrace("{method}()", nameof(UpdatePvValues));
        _settings.LastPvValueUpdate = _dateTimeProvider.DateTimeOffSetNow();
        var gridRequestUrl = _configurationWrapper.CurrentPowerToGridUrl();
        var frontendConfiguration = _configurationWrapper.FrontendConfiguration();
        HttpRequestMessage? gridRequest = default;
        HttpResponseMessage? gridHttpResponse = default;
        if (!string.IsNullOrWhiteSpace(gridRequestUrl) && frontendConfiguration is { GridValueSource: SolarValueSource.Modbus or SolarValueSource.Rest })
        {
            var gridRequestHeaders = _configurationWrapper.CurrentPowerToGridHeaders();
            gridRequest = GenerateHttpRequestMessage(gridRequestUrl, gridRequestHeaders);
            _logger.LogTrace("Request grid power.");
            gridHttpResponse = await GetHttpResponse(gridRequest).ConfigureAwait(false);
            var patternType = frontendConfiguration.GridPowerNodePatternType ?? NodePatternType.Direct;
            var gridJsonPattern = _configurationWrapper.CurrentPowerToGridJsonPattern();
            var gridXmlPattern = _configurationWrapper.CurrentPowerToGridXmlPattern();
            var gridCorrectionFactor = (double)_configurationWrapper.CurrentPowerToGridCorrectionFactor();
            var overage = await GetValueByHttpResponse(gridHttpResponse, gridJsonPattern, gridXmlPattern, gridCorrectionFactor, patternType).ConfigureAwait(false);
            _logger.LogTrace("Overage is {overage}", overage);
            _settings.Overage = overage;
            if (overage != null)
            {
                AddOverageValueToInMemoryList((int)overage);
            }
        }


        var inverterRequestUrl = _configurationWrapper.CurrentInverterPowerUrl();
        HttpRequestMessage? inverterRequest = default;
        HttpResponseMessage? inverterHttpResponse = default;
        if (!string.IsNullOrWhiteSpace(inverterRequestUrl) && frontendConfiguration is { InverterValueSource: SolarValueSource.Modbus or SolarValueSource.Rest})
        {
            var inverterRequestHeaders = _configurationWrapper.CurrentInverterPowerHeaders();
            inverterRequest = GenerateHttpRequestMessage(inverterRequestUrl, inverterRequestHeaders);
            if (IsSameRequest(gridRequest, inverterRequest))
            {
                inverterHttpResponse = gridHttpResponse;
            }
            else
            {
                _logger.LogTrace("Request inverter power.");
                inverterHttpResponse = await GetHttpResponse(inverterRequest).ConfigureAwait(false);
            }
            var patternType = frontendConfiguration.InverterPowerNodePatternType ?? NodePatternType.Direct;
            var inverterJsonPattern = _configurationWrapper.CurrentInverterPowerJsonPattern();
            var inverterXmlPattern = _configurationWrapper.CurrentInverterPowerXmlPattern();
            var inverterCorrectionFactor = (double)_configurationWrapper.CurrentInverterPowerCorrectionFactor();
            var inverterPower = await GetValueByHttpResponse(inverterHttpResponse, inverterJsonPattern, inverterXmlPattern, inverterCorrectionFactor, patternType).ConfigureAwait(false);
            _settings.InverterPower = inverterPower;
        }

        var homeBatterySocRequestUrl = _configurationWrapper.HomeBatterySocUrl();
        HttpRequestMessage? homeBatterySocRequest = default;
        HttpResponseMessage? homeBatterySocHttpResponse = default;
        if (!string.IsNullOrWhiteSpace(homeBatterySocRequestUrl) && frontendConfiguration is { HomeBatteryValuesSource: SolarValueSource.Modbus or SolarValueSource.Rest })
        {
            var homeBatterySocHeaders = _configurationWrapper.HomeBatterySocHeaders();
            homeBatterySocRequest = GenerateHttpRequestMessage(homeBatterySocRequestUrl, homeBatterySocHeaders);
            if (IsSameRequest(gridRequest, homeBatterySocRequest))
            {
                homeBatterySocHttpResponse = gridHttpResponse;
            }
            else if (inverterRequest != default && IsSameRequest(inverterRequest, homeBatterySocRequest))
            {
                homeBatterySocHttpResponse = inverterHttpResponse;
            }
            else
            {
                _logger.LogTrace("Request home battery soc.");
                homeBatterySocHttpResponse = await GetHttpResponse(homeBatterySocRequest).ConfigureAwait(false);
            }
            var patternType = frontendConfiguration.HomeBatterySocNodePatternType ?? NodePatternType.Direct;
            var homeBatterySocJsonPattern = _configurationWrapper.HomeBatterySocJsonPattern();
            var homeBatterySocXmlPattern = _configurationWrapper.HomeBatterySocXmlPattern();
            var homeBatterySocCorrectionFactor = (double)_configurationWrapper.HomeBatterySocCorrectionFactor();
            var homeBatterySoc = await GetValueByHttpResponse(homeBatterySocHttpResponse, homeBatterySocJsonPattern, homeBatterySocXmlPattern, homeBatterySocCorrectionFactor, patternType).ConfigureAwait(false);
            _settings.HomeBatterySoc = homeBatterySoc;
        }

        var homeBatteryPowerRequestUrl = _configurationWrapper.HomeBatteryPowerUrl();
        if (!string.IsNullOrWhiteSpace(homeBatteryPowerRequestUrl) && frontendConfiguration is { HomeBatteryValuesSource: SolarValueSource.Modbus or SolarValueSource.Rest })
        {
            var homeBatteryPowerHeaders = _configurationWrapper.HomeBatteryPowerHeaders();
            var homeBatteryPowerRequest = GenerateHttpRequestMessage(homeBatteryPowerRequestUrl, homeBatteryPowerHeaders);
            HttpResponseMessage? homeBatteryPowerHttpResponse;
            if (IsSameRequest(gridRequest, homeBatteryPowerRequest))
            {
                homeBatteryPowerHttpResponse = gridHttpResponse;
            }
            else if (inverterRequest != default && IsSameRequest(inverterRequest, homeBatteryPowerRequest))
            {
                homeBatteryPowerHttpResponse = inverterHttpResponse;
            }
            else if (homeBatterySocRequest != default && IsSameRequest(homeBatterySocRequest, homeBatteryPowerRequest))
            {
                homeBatteryPowerHttpResponse = homeBatterySocHttpResponse;
            }
            else
            {
                _logger.LogTrace("Request home battery power.");
                homeBatteryPowerHttpResponse = await GetHttpResponse(homeBatteryPowerRequest).ConfigureAwait(false);
            }
            var patternType = frontendConfiguration.HomeBatteryPowerNodePatternType ?? NodePatternType.Direct;
            var homeBatteryPowerJsonPattern = _configurationWrapper.HomeBatteryPowerJsonPattern();
            var homeBatteryPowerXmlPattern = _configurationWrapper.HomeBatteryPowerXmlPattern();
            var homeBatteryPowerCorrectionFactor = (double)_configurationWrapper.HomeBatteryPowerCorrectionFactor();
            var homeBatteryPower = await GetValueByHttpResponse(homeBatteryPowerHttpResponse, homeBatteryPowerJsonPattern, homeBatteryPowerXmlPattern, homeBatteryPowerCorrectionFactor, patternType).ConfigureAwait(false);
            var homeBatteryPowerInversionRequestUrl = _configurationWrapper.HomeBatteryPowerInversionUrl();
            if (!string.IsNullOrEmpty(homeBatteryPowerInversionRequestUrl))
            {
                var homeBatteryPowerInversionHeaders = _configurationWrapper.HomeBatteryPowerInversionHeaders();
                //ToDo: implement setting Headers in frontend
                var homeBatteryPowerInversionRequest = GenerateHttpRequestMessage(homeBatteryPowerInversionRequestUrl, homeBatteryPowerInversionHeaders);
                _logger.LogTrace("Request home battery power inversion.");
                var homeBatteryPowerInversionHttpResponse = await GetHttpResponse(homeBatteryPowerInversionRequest).ConfigureAwait(false);
                var shouldInvertHomeBatteryPowerInt = await GetValueByHttpResponse(homeBatteryPowerInversionHttpResponse, null, null, 1, NodePatternType.Direct).ConfigureAwait(false);
                var shouldInvertHomeBatteryPower = Convert.ToBoolean(shouldInvertHomeBatteryPowerInt);
                if (shouldInvertHomeBatteryPower)
                {
                    homeBatteryPower = -homeBatteryPower;
                }
            }

            _settings.HomeBatteryPower = homeBatteryPower;
        }
    }

    private async Task<int?> GetValueByHttpResponse(HttpResponseMessage? httpResponse, string? jsonPattern, string? xmlPattern,
        double correctionFactor, NodePatternType nodePatternType)
    {
        int? intValue;
        if (httpResponse == null)
        {
            _logger.LogError("HttpResponse is null, extraction of value is not possible");
            return null;
        }
        if (!httpResponse.IsSuccessStatusCode)
        {
            intValue = null;
            _logger.LogError("Could not get value. {statusCode}, {reasonPhrase}", httpResponse.StatusCode,
                httpResponse.ReasonPhrase);
            await _telegramService.SendMessage(
                    $"Getting value did result in statuscode {httpResponse.StatusCode} with reason {httpResponse.ReasonPhrase}")
                .ConfigureAwait(false);
        }
        else
        {
            intValue = await GetIntegerValue(httpResponse, jsonPattern, xmlPattern, correctionFactor, nodePatternType).ConfigureAwait(false);
        }

        return intValue;
    }

    private async Task<HttpResponseMessage> GetHttpResponse(HttpRequestMessage request)
    {
        _logger.LogTrace("{method}({request}) [called by {callingMethod}]", nameof(GetHttpResponse), request, new StackTrace().GetFrame(1)?.GetMethod()?.Name);
        using var httpClient = new HttpClient();
        var response = await httpClient.SendAsync(request).ConfigureAwait(false);
        return response;
    }

    private static HttpRequestMessage GenerateHttpRequestMessage(string? gridRequestUrl, Dictionary<string, string> requestHeaders)
    {
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

        return request;
    }

    public int GetAveragedOverage()
    {
        _logger.LogTrace("{method}()", nameof(GetAveragedOverage));
        long weightedSum = 0;
        if (_settings.Overage == null)
        {
            return int.MinValue;
        }
        _logger.LogTrace("Build weighted average of {count} values", _inMemoryValues.OverageValues.Count);
        for (var i = 0; i < _inMemoryValues.OverageValues.Count; i++)
        {
            _logger.LogTrace("Power Value: {value}", _inMemoryValues.OverageValues[i]);
            weightedSum += _inMemoryValues.OverageValues[i] * (i + 1);
            _logger.LogTrace("weightedSum: {value}", weightedSum);
        }
        var weightedCount = _inMemoryValues.OverageValues.Count * (_inMemoryValues.OverageValues.Count + 1) / 2;
        if (weightedCount == 0)
        {
            var powerDefaultValue = int.MinValue;
            _logger.LogWarning("There are no power values available, use default value of {defaultValue}", powerDefaultValue);
            return int.MinValue;
        }
        return (int)(weightedSum / weightedCount);
    }

    public void AddOverageValueToInMemoryList(int overage)
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

    internal bool IsSameRequest(HttpRequestMessage? httpRequestMessage1, HttpRequestMessage httpRequestMessage2)
    {
        if (httpRequestMessage1 == null)
        {
            return false;
        }
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



    private async Task<int?> GetIntegerValue(HttpResponseMessage response, string? jsonPattern, string? xmlPattern, double correctionFactor,
        NodePatternType nodePatternType)
    {
        _logger.LogTrace("{method}({httpResonse}, {jsonPattern}, {xmlPattern}, {correctionFactor})",
            nameof(GetIntegerValue), response, jsonPattern, xmlPattern, correctionFactor);

        var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        return GetIntegerValueByString(result, jsonPattern, xmlPattern, correctionFactor, nodePatternType);
    }

    public int? GetIntegerValueByString(string valueString, string? jsonPattern, string? xmlPattern, double correctionFactor,
        NodePatternType nodePatternType)
    {
        _logger.LogTrace("{method}({valueString}, {jsonPattern}, {xmlPattern}, {correctionFactor})",
            nameof(GetIntegerValueByString), valueString, jsonPattern, xmlPattern, correctionFactor);
        var pattern = string.Empty;

        if (nodePatternType == NodePatternType.Json)
        {
            pattern = jsonPattern;
        }
        else if (nodePatternType == NodePatternType.Xml)
        {
            pattern = xmlPattern;
        }

        var doubleValue = GetValueFromResult(pattern, valueString, nodePatternType, true);

        return (int?)(doubleValue * correctionFactor);
    }

    
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="pattern"></param>
    /// <param name="result"></param>
    /// <param name="patternType"></param>
    /// <param name="isGridValue">true if grid meter value is requested, false if inverter value is requested</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="ArgumentNullException"></exception>
    internal double GetValueFromResult(string? pattern, string result, NodePatternType patternType, bool isGridValue)
    {
        switch (patternType)
        {
            case NodePatternType.Json:
                _logger.LogTrace("Extract overage value from json {result} with {pattern}", result, pattern);
                result = (JObject.Parse(result).SelectToken(pattern ?? throw new ArgumentNullException(nameof(pattern))) ??
                          throw new InvalidOperationException("Could not find token by pattern")).Value<string>() ?? throw new InvalidOperationException("Extracted Json Value is null");
                break;
            case NodePatternType.Xml:
                _logger.LogTrace("Extract overage value from xml {result} with {pattern}", result, pattern);
                var xmlDocument = new XmlDocument();
                xmlDocument.LoadXml(result);
                var nodes = xmlDocument.SelectNodes(pattern ?? throw new ArgumentNullException(nameof(pattern))) ?? throw new InvalidOperationException("Could not find any nodes by pattern");
                switch (nodes.Count)
                {
                    case < 1:
                        throw new InvalidOperationException($"Could not find any nodes with pattern {pattern}");
                    case > 2:
                        var xmlAttributeHeaderName = (isGridValue
                            ? _configurationWrapper.CurrentPowerToGridXmlAttributeHeaderName()
                            : _configurationWrapper.CurrentInverterPowerXmlAttributeHeaderName())
                              ?? throw new InvalidOperationException("Could not get xmlAttributeHeaderName");

                        var xmlAttributeHeaderValue = (isGridValue
                            ? _configurationWrapper.CurrentPowerToGridXmlAttributeHeaderValue()
                            : _configurationWrapper.CurrentInverterPowerXmlAttributeHeaderValue())
                              ?? throw new InvalidOperationException("Could not get xmlAttributeHeaderValue");

                        var xmlAttributeValueName = (isGridValue
                            ? _configurationWrapper.CurrentPowerToGridXmlAttributeValueName()
                            : _configurationWrapper.CurrentInverterPowerXmlAttributeValueName())
                              ?? throw new InvalidOperationException("Could not get xmlAttributeValueName");

                        for (var i = 0; i < nodes.Count; i++)
                        {
                            if (nodes[i]?.Attributes?[xmlAttributeHeaderName]?.Value == xmlAttributeHeaderValue)
                            {
                                result = nodes[i]?.Attributes?[xmlAttributeValueName]?.Value ?? "0";
                                break;
                            }
                        }
                        break;
                    case 1:
                        result = nodes[0]?.LastChild?.Value ?? "0";
                        break;
                }
                break;
        }

        return GetdoubleFromStringResult(result);
    }

    internal double GetdoubleFromStringResult(string? inputString)
    {
        _logger.LogTrace("{method}({param})", nameof(GetdoubleFromStringResult), inputString);
        return double.Parse(inputString ?? throw new ArgumentNullException(nameof(inputString)), CultureInfo.InvariantCulture);
    }
}
