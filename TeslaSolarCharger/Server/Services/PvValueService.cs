using Newtonsoft.Json.Linq;
using Quartz.Util;
using System.Globalization;
using System.Xml;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Enums;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class PvValueService : IPvValueService
{
    private readonly ILogger<PvValueService> _logger;
    private readonly ISettings _settings;
    private readonly IInMemoryValues _inMemoryValues;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly ITelegramService _telegramService;

    public PvValueService(ILogger<PvValueService> logger, ISettings settings,
        IInMemoryValues inMemoryValues, IConfigurationWrapper configurationWrapper, ITelegramService telegramService)
    {
        _logger = logger;
        _settings = settings;
        _inMemoryValues = inMemoryValues;
        _configurationWrapper = configurationWrapper;
        _telegramService = telegramService;
    }

    public async Task UpdatePvValues()
    {
        _logger.LogTrace("{method}()", nameof(UpdatePvValues));

        var gridRequestUrl = _configurationWrapper.CurrentPowerToGridUrl();
        HttpRequestMessage? gridRequest = default;
        HttpResponseMessage? gridHttpResponse = default;
        if (!string.IsNullOrWhiteSpace(gridRequestUrl))
        {
            var gridRequestHeaders = _configurationWrapper.CurrentPowerToGridHeaders();
            gridRequest = GenerateHttpRequestMessage(gridRequestUrl, gridRequestHeaders);
            gridHttpResponse = await GetHttpResponse(gridRequest).ConfigureAwait(false);
            var gridJsonPattern = _configurationWrapper.CurrentPowerToGridJsonPattern();
            var gridXmlPattern = _configurationWrapper.CurrentPowerToGridXmlPattern();
            var gridCorrectionFactor = (double)_configurationWrapper.CurrentPowerToGridCorrectionFactor();
            var overage = await GetValueByHttpResponse(gridHttpResponse, gridJsonPattern, gridXmlPattern, gridCorrectionFactor).ConfigureAwait(false);
            _logger.LogDebug("Overage is {overage}", overage);
            _settings.Overage = overage;
            if (overage != null)
            {
                AddOverageValueToInMemoryList((int)overage);
            }
        }


        var inverterRequestUrl = _configurationWrapper.CurrentInverterPowerUrl();
        HttpRequestMessage? inverterRequest = default;
        HttpResponseMessage? inverterHttpResponse = default;
        if (!string.IsNullOrWhiteSpace(inverterRequestUrl))
        {
            var inverterRequestHeaders = _configurationWrapper.CurrentInverterPowerHeaders();
            inverterRequest = GenerateHttpRequestMessage(inverterRequestUrl, inverterRequestHeaders);
            if (IsSameRequest(gridRequest, inverterRequest))
            {
                inverterHttpResponse = gridHttpResponse;
            }
            else
            {
                inverterHttpResponse = await GetHttpResponse(inverterRequest).ConfigureAwait(false);
            }
            var inverterJsonPattern = _configurationWrapper.CurrentInverterPowerJsonPattern();
            var inverterXmlPattern = _configurationWrapper.CurrentInverterPowerXmlPattern();
            var inverterCorrectionFactor = (double)_configurationWrapper.CurrentInverterPowerCorrectionFactor();
            var inverterPower = await GetValueByHttpResponse(inverterHttpResponse, inverterJsonPattern, inverterXmlPattern, inverterCorrectionFactor).ConfigureAwait(false);
            _settings.InverterPower = inverterPower;
        }

        var homeBatterySocRequestUrl = _configurationWrapper.HomeBatterySocUrl();
        HttpRequestMessage? homeBatterySocRequest = default;
        HttpResponseMessage? homeBatterySocHttpResponse = default;
        if (!string.IsNullOrWhiteSpace(homeBatterySocRequestUrl))
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
                homeBatterySocHttpResponse = await GetHttpResponse(homeBatterySocRequest).ConfigureAwait(false);
            }
            var homeBatterySocJsonPattern = _configurationWrapper.HomeBatterySocJsonPattern();
            var homeBatterySocXmlPattern = _configurationWrapper.HomeBatterySocXmlPattern();
            var homeBatterySocCorrectionFactor = (double)_configurationWrapper.HomeBatterySocCorrectionFactor();
            var homeBatterySoc = await GetValueByHttpResponse(homeBatterySocHttpResponse, homeBatterySocJsonPattern, homeBatterySocXmlPattern, homeBatterySocCorrectionFactor).ConfigureAwait(false);
            _settings.HomeBatterySoc = homeBatterySoc;
        }

        var homeBatteryPowerRequestUrl = _configurationWrapper.HomeBatteryPowerUrl();
        if (!string.IsNullOrWhiteSpace(homeBatteryPowerRequestUrl))
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
                homeBatteryPowerHttpResponse = await GetHttpResponse(homeBatteryPowerRequest).ConfigureAwait(false);
            }
            var homeBatteryPowerJsonPattern = _configurationWrapper.HomeBatteryPowerJsonPattern();
            var homeBatteryPowerXmlPattern = _configurationWrapper.HomeBatteryPowerXmlPattern();
            var homeBatteryPowerCorrectionFactor = (double)_configurationWrapper.HomeBatteryPowerCorrectionFactor();
            var homeBatteryPower = await GetValueByHttpResponse(homeBatteryPowerHttpResponse, homeBatteryPowerJsonPattern, homeBatteryPowerXmlPattern, homeBatteryPowerCorrectionFactor).ConfigureAwait(false);
            _settings.HomeBatteryPower = homeBatteryPower;
        }
    }

    private async Task<int?> GetValueByHttpResponse(HttpResponseMessage? httpResponse, string? jsonPattern, string? xmlPattern, double correctionFactor)
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
            intValue = await GetIntegerValue(httpResponse, jsonPattern, xmlPattern, correctionFactor).ConfigureAwait(false);
        }

        return intValue;
    }

    private async Task<HttpResponseMessage> GetHttpResponse(HttpRequestMessage request)
    {
        _logger.LogTrace("{method}({request})", nameof(GetHttpResponse), request);
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



    private async Task<int?> GetIntegerValue(HttpResponseMessage response, string? jsonPattern, string? xmlPattern, double correctionFactor)
    {
        _logger.LogTrace("{method}({httpResonse}, {jsonPattern}, {xmlPattern}, {correctionFactor})",
            nameof(GetIntegerValue), response, jsonPattern, xmlPattern, correctionFactor);

        var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        return GetIntegerValueByString(result, jsonPattern, xmlPattern, correctionFactor);
    }

    public int? GetIntegerValueByString(string valueString, string? jsonPattern, string? xmlPattern, double correctionFactor)
    {
        _logger.LogTrace("{method}({valueString}, {jsonPattern}, {xmlPattern}, {correctionFactor})",
            nameof(GetIntegerValueByString), valueString, jsonPattern, xmlPattern, correctionFactor);
        var pattern = "";
        var nodePatternType = DecideNodePatternType(jsonPattern, xmlPattern);

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

    internal NodePatternType DecideNodePatternType(string? jsonPattern, string? xmlPattern)
    {
        _logger.LogTrace("{method}({param1}, {param2})", nameof(DecideNodePatternType), jsonPattern, xmlPattern);
        NodePatternType nodePatternType;
        if (!jsonPattern.IsNullOrWhiteSpace())
        {
            nodePatternType = NodePatternType.Json;
        }
        else if (!xmlPattern.IsNullOrWhiteSpace())
        {
            nodePatternType = NodePatternType.Xml;
        }
        else
        {
            nodePatternType = NodePatternType.None;
        }
        _logger.LogDebug("Node pattern type is {nodePatternType}", nodePatternType);
        return nodePatternType;
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
                _logger.LogDebug("Extract overage value from json {result} with {pattern}", result, pattern);
                result = (JObject.Parse(result).SelectToken(pattern ?? throw new ArgumentNullException(nameof(pattern))) ??
                          throw new InvalidOperationException("Could not find token by pattern")).Value<string>() ?? throw new InvalidOperationException("Extracted Json Value is null");
                break;
            case NodePatternType.Xml:
                _logger.LogDebug("Extract overage value from xml {result} with {pattern}", result, pattern);
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
