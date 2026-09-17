using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Text;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Rest.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Generic;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.GenericRest;

/// <summary>
/// Value gathering for all vendors defined via JSON REST maps in <see cref="JsonRestTemplateDefinitions"/>.
/// </summary>
public class GenericJsonRestTemplateValueSetupService : IRefreshableValueSetupService
{
    private readonly ILogger<GenericJsonRestTemplateValueSetupService> _logger;
    private readonly IConstants _constants;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ITemplateValueConfigurationService _templateValueConfigurationService;
    private readonly IHttpClientFactory _httpClientFactory;

    public GenericJsonRestTemplateValueSetupService(ILogger<GenericJsonRestTemplateValueSetupService> logger, IConstants constants,
        IServiceScopeFactory serviceScopeFactory, ITemplateValueConfigurationService templateValueConfigurationService,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _constants = constants;
        _serviceScopeFactory = serviceScopeFactory;
        _templateValueConfigurationService = templateValueConfigurationService;
        _httpClientFactory = httpClientFactory;
    }

    public ConfigurationType ConfigurationType => ConfigurationType.TemplateValue;

    public async Task<List<DelegateRefreshableValue<decimal>>> GetDecimalRefreshableValuesAsync(TimeSpan defaultInterval,
        List<int> configurationIds)
    {
        _logger.LogTrace("{method}({defaultInterval})", nameof(GetDecimalRefreshableValuesAsync), defaultInterval);
        var gatherTypes = JsonRestTemplateDefinitions.Definitions.Keys.ToList();
        Expression<Func<TemplateValueConfiguration, bool>> expression = c =>
            gatherTypes.Contains(c.GatherType) && (configurationIds.Count == 0 || configurationIds.Contains(c.Id));
        var configs = await _templateValueConfigurationService
            .GetConfigurationsByPredicateAsync(expression).ConfigureAwait(false);
        var result = new List<DelegateRefreshableValue<decimal>>();
        foreach (var config in configs)
        {
            if (config.Configuration == default || config.GatherType == default)
            {
                _logger.LogError("Template configuration with ID {id} has empty configuration", config.Id);
                continue;
            }
            var typedConfig = config.Configuration.ToObject<DtoGenericRestTemplateValueConfiguration>();
            if (typedConfig == default || string.IsNullOrEmpty(typedConfig.Host))
            {
                _logger.LogError("Could not deserialize configuration {gatherType} for ID {id}. Json is: {json}",
                    config.GatherType, config.Id, config.Configuration.ToString(Formatting.None));
                continue;
            }
            var definition = JsonRestTemplateDefinitions.Definitions[config.GatherType.Value];
            var refreshable = new DelegateRefreshableValue<decimal>(
                _serviceScopeFactory,
                async ct =>
                {
                    var values = new Dictionary<ValueKey, decimal>();
                    var resultId = 0;
                    var httpClient = _httpClientFactory.CreateClient(StaticConstants.HttpClientNameShortTimeout);
                    foreach (var valueRead in definition.ValueReads)
                    {
                        ct.ThrowIfCancellationRequested();
                        var uri = ResolveUriTemplate(valueRead.UriTemplate, typedConfig);
                        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, uri);
                        ApplyAuthHeaders(httpRequest, definition.ValueAuthType, typedConfig);
                        using var httpResponse = await httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);
                        httpResponse.EnsureSuccessStatusCode();
                        var json = await httpResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                        var root = JToken.Parse(json);
                        foreach (var value in valueRead.Values)
                        {
                            resultId++;
                            var token = root.SelectToken(value.JsonPath);
                            if (token == default || token.Type == JTokenType.Null)
                            {
                                throw new InvalidDataException($"Path {value.JsonPath} not found in response of {uri}");
                            }
                            var rawValue = token.Value<decimal>();
                            var calculatedValue = rawValue * value.CorrectionFactor
                                                  * (value.Operator == ValueOperator.Minus ? -1 : 1);
                            var valueKey = new ValueKey(value.UsedFor, null, resultId);
                            values.TryAdd(valueKey, 0m);
                            values[valueKey] += calculatedValue;
                        }
                    }
                    return new(values);
                },
                defaultInterval,
                _constants.SolarHistoricValueCapacity,
                new(config.Id, ConfigurationType.TemplateValue)
            );
            result.Add(refreshable);
        }
        return result;
    }

    public static string ResolveUriTemplate(string uriTemplate, DtoGenericRestTemplateValueConfiguration config)
    {
        return uriTemplate
            .Replace("{host}", config.Host)
            .Replace("{port}", config.Port.ToString())
            .Replace("{deviceId}", config.DeviceId.ToString())
            .Replace("{maxChargePowerW}", config.MaxBatteryChargePowerW.ToString());
    }

    /// <summary>
    /// Auth has to be set per request: the <see cref="HttpClient"/> comes from <see cref="IHttpClientFactory"/> and is
    /// shared, so credentials of one configuration must not end up on its DefaultRequestHeaders.
    /// </summary>
    public static void ApplyAuthHeaders(HttpRequestMessage request, JsonRestAuthType authType,
        DtoGenericRestTemplateValueConfiguration config)
    {
        if (authType == JsonRestAuthType.Basic)
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.Username}:{config.Password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }
    }
}
