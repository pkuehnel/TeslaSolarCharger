using System.Globalization;
using System.Xml;
using Newtonsoft.Json.Linq;
using Quartz.Util;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Enums;
using TeslaSolarCharger.Shared.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class GridService : IGridService
{
    private readonly ILogger<GridService> _logger;
    private readonly IConfigurationWrapper _configurationWrapper;

    public GridService(ILogger<GridService> logger, IConfigurationWrapper configurationWrapper)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
    }

    public async Task<int?> GetCurrentOverage(HttpResponseMessage response)
    {
        _logger.LogTrace("{method}()", nameof(GetCurrentOverage));

        var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        var pattern = "";
        var jsonPattern = _configurationWrapper.CurrentPowerToGridJsonPattern();
        var xmlPattern = _configurationWrapper.CurrentPowerToGridXmlPattern();
        var nodePatternType = DecideNotePatternType(jsonPattern, xmlPattern);

        if (nodePatternType == NodePatternType.Json)
        {
            pattern = jsonPattern;
        }
        else if (nodePatternType == NodePatternType.Xml)
        {
            pattern = xmlPattern;
        }

        var overage = GetValueFromResult(pattern, result, nodePatternType, true);

        return (int?)(overage * (double) _configurationWrapper.CurrentPowerToGridCorrectionFactor());
    }

    internal NodePatternType DecideNotePatternType(string? jsonPattern, string? xmlPattern)
    {
        _logger.LogTrace("{method}({param1}, {param2})", nameof(DecideNotePatternType), jsonPattern, xmlPattern);
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

    public async Task<int?> GetCurrentInverterPower(HttpResponseMessage response)
    {
        _logger.LogTrace("{method}()", nameof(GetCurrentInverterPower));
        
        var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        var pattern = "";
        var jsonPattern = _configurationWrapper.CurrentInverterPowerJsonPattern();
        var xmlPattern = _configurationWrapper.CurrentInverterPowerXmlPattern();
        var nodePatternType = DecideNotePatternType(jsonPattern, xmlPattern);

        if (nodePatternType == NodePatternType.Json)
        {
            pattern = jsonPattern;
        }
        else if (nodePatternType == NodePatternType.Xml)
        {
            pattern = xmlPattern;
        }

        var power = (double?)GetValueFromResult(pattern, result, nodePatternType, false);

        return (int?)(power * (double)_configurationWrapper.CurrentInverterPowerCorrectionFactor());
    }

    public async Task<int?> GetCurrentHomeBatterySoc(HttpResponseMessage response)
    {
        _logger.LogTrace("{method}()", nameof(GetCurrentHomeBatterySoc));

        var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        var pattern = "";
        var jsonPattern = _configurationWrapper.HomeBatterySocJsonPattern();
        var xmlPattern = _configurationWrapper.HomeBatterySocXmlPattern();
        var nodePatternType = DecideNotePatternType(jsonPattern, xmlPattern);

        if (nodePatternType == NodePatternType.Json)
        {
            pattern = jsonPattern;
        }
        else if (nodePatternType == NodePatternType.Xml)
        {
            pattern = xmlPattern;
        }

        var power = (double?)GetValueFromResult(pattern, result, nodePatternType, false);

        return (int?)(power * (double)_configurationWrapper.HomeBatterySocCorrectionFactor());
    }

    public async Task<int?> GetCurrentHomeBatteryPower(HttpResponseMessage response)
    {
        _logger.LogTrace("{method}()", nameof(GetCurrentHomeBatteryPower));

        var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        var pattern = "";
        var jsonPattern = _configurationWrapper.HomeBatteryPowerJsonPattern();
        var xmlPattern = _configurationWrapper.HomeBatteryPowerXmlPattern();
        var nodePatternType = DecideNotePatternType(jsonPattern, xmlPattern);

        if (nodePatternType == NodePatternType.Json)
        {
            pattern = jsonPattern;
        }
        else if (nodePatternType == NodePatternType.Xml)
        {
            pattern = xmlPattern;
        }

        var power = (double?)GetValueFromResult(pattern, result, nodePatternType, false);

        return (int?)(power * (double)_configurationWrapper.HomeBatteryPowerCorrectionFactor());
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
    internal double GetValueFromResult(string pattern, string result, NodePatternType patternType, bool isGridValue)
    {
        switch (patternType)
        {
            case NodePatternType.Json:
                _logger.LogDebug("Extract overage value from json {result} with {pattern}", result, pattern);
                result = (JObject.Parse(result).SelectToken(pattern) ??
                          throw new InvalidOperationException("Could not find token by pattern")).Value<string>() ?? throw new InvalidOperationException("Extracted Json Value is null");
                break;
            case NodePatternType.Xml:
                _logger.LogDebug("Extract overage value from xml {result} with {pattern}", result, pattern);
                var xmlDocument = new XmlDocument();
                xmlDocument.LoadXml(result);
                var nodes = xmlDocument.SelectNodes(pattern) ?? throw new InvalidOperationException("Could not find any nodes by pattern");
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

                        for (int i = 0; i < nodes.Count; i++)
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