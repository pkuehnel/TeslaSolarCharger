using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;
using TeslaSolarCharger.SharedModel.Enums;

[assembly: InternalsVisibleTo("TeslaSolarCharger.Tests")]
namespace TeslaSolarCharger.Services.Services.Rest;

public class RestValueExecutionService(
    ILogger<RestValueConfigurationService> logger,
    ISettings settings,
    IRestValueConfigurationService restValueConfigurationService,
    IConfigurationWrapper configurationWrapper,
    IResultValueCalculationService resultValueCalculationService) : IRestValueExecutionService
{
    /// <summary>
    /// Get result for each configuration ID
    /// </summary>
    /// <param name="config">Rest Value configuration</param>
    /// <returns>Dictionary with with resultConfiguration as key and resulting value as Value</returns>
    /// <exception cref="InvalidOperationException">Throw if request results in not success status code</exception>
    public async Task<string> GetResult(DtoFullRestValueConfiguration config)
    {
        logger.LogTrace("{method}({@config})", nameof(GetResult), config);
        var httpClientHandler = new HttpClientHandler();

        if (configurationWrapper.ShouldIgnoreSslErrors())
        {
            logger.LogWarning("PV Value SSL errors are ignored.");
            httpClientHandler.ServerCertificateCustomValidationCallback = MyRemoteCertificateValidationCallback;
        }
        using var client = new HttpClient(httpClientHandler);
        client.Timeout = TimeSpan.FromSeconds(1);
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
            logger.LogError("Requesting string with url {requestUrl} did result in non success status code: {statusCode} {content}", config.Url, response.StatusCode, contentString);
            throw new InvalidOperationException($"Requesting string with url {config.Url} did result in non success status code: {response.StatusCode} {contentString}");
        }

        return contentString;
    }
    private bool MyRemoteCertificateValidationCallback(HttpRequestMessage requestMessage, X509Certificate2? certificate, X509Chain? chain, SslPolicyErrors sslErrors)
    {
        return true; // Ignoriere alle Zertifikatfehler
    }

    public decimal GetValue(string responseString, NodePatternType configNodePatternType, DtoRestValueResultConfiguration resultConfig)
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
        return resultValueCalculationService.MakeCalculationsOnRawValue(resultConfig.CorrectionFactor, resultConfig.Operator, rawValue);
    }

    public async Task<List<DtoValueConfigurationOverview>> GetRestValueOverviews()
    {
        logger.LogTrace("{method}()", nameof(GetRestValueOverviews));
        var restValueConfigurations = await restValueConfigurationService.GetFullRestValueConfigurationsByPredicate(c => true).ConfigureAwait(false);
        var results = new List<DtoValueConfigurationOverview>();
        foreach (var dtoFullRestValueConfiguration in restValueConfigurations)
        {
            string? result;
            var resultConfigurations = await restValueConfigurationService.GetRestResultConfigurationByPredicate(c => c.RestValueConfigurationId == dtoFullRestValueConfiguration.Id).ConfigureAwait(false);
            var overviewElement = new DtoValueConfigurationOverview
            {
                Id = dtoFullRestValueConfiguration.Id,
                Heading = dtoFullRestValueConfiguration.Url,
            };
            results.Add(overviewElement);
            try
            {
                result = await GetResult(dtoFullRestValueConfiguration).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting result for rest configuration {id}", dtoFullRestValueConfiguration.Id);
                result = null;
            }
            foreach (var resultConfiguration in resultConfigurations)
            {
                var dtoRestValueResult = new DtoOverviewValueResult { Id = resultConfiguration.Id, UsedFor = resultConfiguration.UsedFor, };
                try
                {
                    dtoRestValueResult.CalculatedValue = result == null ? null : GetValue(result, dtoFullRestValueConfiguration.NodePatternType, resultConfiguration); ;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error getting value for rest configuration {id}", resultConfiguration.Id);
                    continue;
                }
                finally
                {
                    overviewElement.Results.Add(dtoRestValueResult);
                }
            }
        }

        return results;
    }

    public async Task<string> DebugRestValueConfiguration(DtoFullRestValueConfiguration config)
    {
        logger.LogTrace("{method}({@config})", nameof(DebugRestValueConfiguration), config);
        try
        {
            return await GetResult(config);
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }
}
