using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Xml;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;
using TeslaSolarCharger.SharedModel.Enums;


[assembly: InternalsVisibleTo("TeslaSolarCharger.Tests")]
namespace TeslaSolarCharger.Services.Services;

public class RestValueExecutionService(
    ILogger<RestValueConfigurationService> logger, ISettings settings) : IRestValueExecutionService
{
    /// <summary>
    /// Get result for each configuration ID
    /// </summary>
    /// <param name="config">Rest Value configuration</param>
    /// <returns>Dictionary with with resultConfiguration as key and resulting value as Value</returns>
    /// <exception cref="InvalidOperationException">Throw if request results in not success status code</exception>
    public async Task<Dictionary<int, decimal>> GetResult(DtoFullRestValueConfiguration config)
    {
        logger.LogTrace("{method}({@config})", nameof(GetResult), config);
        var client = new HttpClient();
        var request = new HttpRequestMessage(new HttpMethod(config.HttpMethod.ToString()), config.Url);
        foreach (var header in config.Headers)
        {
            request.Headers.Add(header.Key, header.Value);
        }
        var response = await client.SendAsync(request).ConfigureAwait(false);
        var contentString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        settings.RawRestRequestResults[config.Id] = contentString;
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Requesting JSON Result with url {requestUrl} did result in non success status code: {statusCode} {content}", config.Url, response.StatusCode, contentString);
            throw new InvalidOperationException($"Requesting JSON Result with url {config.Url} did result in non success status code: {response.StatusCode} {contentString}");
        }
        var results = new Dictionary<int, decimal>();
        
        foreach (var resultConfig in config.RestValueResultConfigurations)
        {
            var value = GetValue(contentString, config.NodePatternType, resultConfig);
            settings.CalculatedRestValues[resultConfig.Id] = value;
            results.Add(resultConfig.Id, value);
        }
        return results;
    }

    internal decimal GetValue(string responseString, NodePatternType configNodePatternType, DtoRestValueResultConfiguration resultConfig)
    {
        logger.LogTrace("{method}({responseString}, {configNodePatternType}, {@resultConfig})", nameof(GetValue), responseString, configNodePatternType, resultConfig);
        decimal rawValue;
        switch (configNodePatternType)
        {
            case NodePatternType.Direct:
                settings.RawRestValues[resultConfig.Id] = responseString;
                rawValue = decimal.Parse(responseString, NumberStyles.Number, CultureInfo.InvariantCulture);
                break;
            case NodePatternType.Json:
                var jsonTokenString = (JObject.Parse(responseString).SelectToken(resultConfig.NodePattern ?? throw new ArgumentNullException(nameof(resultConfig.NodePattern))) ??
                          throw new InvalidOperationException("Could not find token by pattern")).Value<string>() ?? "0";
                settings.RawRestValues[resultConfig.Id] = jsonTokenString;
                rawValue = decimal.Parse(jsonTokenString, NumberStyles.Number, CultureInfo.InvariantCulture);
                break;
            case NodePatternType.Xml:
                var xmlDocument = new XmlDocument();
                xmlDocument.LoadXml(responseString);
                var nodes = xmlDocument.SelectNodes(resultConfig.NodePattern ?? throw new ArgumentNullException(nameof(resultConfig.NodePattern))) ?? throw new InvalidOperationException("Could not find any nodes by pattern");
                var xmlTokenString = string.Empty;
                switch (nodes.Count)
                {
                    case < 1:
                        throw new InvalidOperationException($"Could not find any nodes with pattern {resultConfig.NodePattern}");
                    case 1:
                        xmlTokenString = nodes[0]?.LastChild?.Value ?? "0";
                        break;
                    case > 2:
                        for (var i = 0; i < nodes.Count; i++)
                        {
                            if (nodes[i]?.Attributes?[resultConfig.XmlAttributeHeaderName ?? throw new ArgumentNullException(nameof(resultConfig.XmlAttributeHeaderName))]?.Value == resultConfig.XmlAttributeHeaderValue)
                            {
                                xmlTokenString = nodes[i]?.Attributes?[resultConfig.XmlAttributeValueName ?? throw new ArgumentNullException(nameof(resultConfig.XmlAttributeValueName))]?.Value ?? "0";
                                break;
                            }
                        }
                        break;
                }
                settings.RawRestValues[resultConfig.Id] = xmlTokenString;
                rawValue = decimal.Parse(xmlTokenString, NumberStyles.Number, CultureInfo.InvariantCulture);
                break;
            default:
                throw new InvalidOperationException($"NodePatternType {configNodePatternType} not supported");
        }
        return MakeCalculationsOnRawValue(resultConfig.CorrectionFactor, resultConfig.Operator, rawValue);
    }

    internal decimal MakeCalculationsOnRawValue(decimal correctionFactor, ValueOperator valueOperator, decimal rawValue)
    {
        rawValue = correctionFactor * rawValue;
        switch (valueOperator)
        {
            case ValueOperator.Plus:
                return rawValue;
            case ValueOperator.Minus:
                return -rawValue;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
