using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Xml;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;
using TeslaSolarCharger.SharedModel.Enums;


[assembly: InternalsVisibleTo("TeslaSolarCharger.Tests")]
namespace TeslaSolarCharger.Services.Services;

public class RestValueExecutionService(
    ILogger<RestValueConfigurationService> logger) : IRestValueExecutionService
{
    /// <summary>
    /// Get result for each configuration ID
    /// </summary>
    /// <param name="config">Rest Value configuration</param>
    /// <param name="headers">Headers for REST request</param>
    /// <param name="resultConfigurations">Configurations to extract the values</param>
    /// <returns>Dictionary with with resultConfiguration as key and resulting value as Value</returns>
    /// <exception cref="InvalidOperationException">Throw if request results in not success status code</exception>
    public async Task<Dictionary<int, decimal>> GetResult(DtoRestValueConfiguration config,
        List<DtoRestValueConfigurationHeader> headers,
        List<DtoRestValueResultConfiguration> resultConfigurations)
    {
        logger.LogTrace("{method}({@config}, {@headers}, {resultConfigurations})", nameof(GetResult), config, headers, resultConfigurations);
        var client = new HttpClient();
        var request = new HttpRequestMessage(new HttpMethod(config.HttpMethod.ToString()), config.Url);
        foreach (var header in headers)
        {
            request.Headers.Add(header.Key, header.Value);
        }
        var response = await client.SendAsync(request).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var contentString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            logger.LogError("Requesting JSON Result with url {requestUrl} did result in non success status code: {statusCode} {content}", config.Url, response.StatusCode, contentString);
            throw new InvalidOperationException($"Requesting JSON Result with url {config.Url} did result in non success status code: {response.StatusCode} {contentString}");
        }
        var results = new Dictionary<int, decimal>();
        foreach (var resultConfig in resultConfigurations)
        {
            var contentString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            results.Add(resultConfig.Id, GetValue(contentString, config.NodePatternType, resultConfig));
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
                rawValue = decimal.Parse(responseString, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
                break;
            case NodePatternType.Json:
                var jsonTokenString = (JObject.Parse(responseString).SelectToken(resultConfig.NodePattern ?? throw new ArgumentNullException(nameof(resultConfig.NodePattern))) ??
                          throw new InvalidOperationException("Could not find token by pattern")).Value<string>() ?? "0";
                rawValue = decimal.Parse(jsonTokenString, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
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
                rawValue = decimal.Parse(xmlTokenString, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
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
