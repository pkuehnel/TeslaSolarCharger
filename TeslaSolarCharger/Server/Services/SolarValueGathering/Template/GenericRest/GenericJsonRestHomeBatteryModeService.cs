using Newtonsoft.Json;
using System.Linq.Expressions;
using System.Text;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.HomeBatteryControl;
using TeslaSolarCharger.Server.Services.HomeBatteryControl.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.Contracts;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Generic;
using TeslaSolarCharger.Shared.Resources;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.GenericRest;

/// <summary>
/// Home battery control for all vendors defined via JSON REST maps in <see cref="JsonRestTemplateDefinitions"/>
/// that include a battery control definition.
/// </summary>
public class GenericJsonRestHomeBatteryModeService : IHomeBatteryModeSetupService
{
    private readonly ILogger<GenericJsonRestHomeBatteryModeService> _logger;
    private readonly ITemplateValueConfigurationService _templateValueConfigurationService;
    private readonly IHttpClientFactory _httpClientFactory;

    public GenericJsonRestHomeBatteryModeService(ILogger<GenericJsonRestHomeBatteryModeService> logger,
        ITemplateValueConfigurationService templateValueConfigurationService,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _templateValueConfigurationService = templateValueConfigurationService;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<List<DtoHomeBatteryModeController>> GetControllersAsync(CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}()", nameof(GetControllersAsync));
        var gatherTypes = JsonRestTemplateDefinitions.Definitions
            .Where(d => d.Value.BatteryControl != default)
            .Select(d => d.Key)
            .ToList();
        Expression<Func<TemplateValueConfiguration, bool>> expression = c => gatherTypes.Contains(c.GatherType);
        var configs = await _templateValueConfigurationService
            .GetConfigurationsByPredicateAsync(expression).ConfigureAwait(false);
        var result = new List<DtoHomeBatteryModeController>();
        foreach (var config in configs)
        {
            if (config.Configuration == default || config.GatherType == default)
            {
                continue;
            }
            var typedConfig = config.Configuration.ToObject<DtoGenericRestTemplateValueConfiguration>();
            if (typedConfig == default)
            {
                _logger.LogError("Could not deserialize configuration {gatherType} for ID {id}. Json is: {json}",
                    config.GatherType, config.Id, config.Configuration.ToString(Formatting.None));
                continue;
            }
            if (!typedConfig.EnableHomeBatteryControl || string.IsNullOrEmpty(typedConfig.Host))
            {
                continue;
            }
            var definition = JsonRestTemplateDefinitions.Definitions[config.GatherType.Value];
            var batteryControl = definition.BatteryControl!;
            result.Add(new DtoHomeBatteryModeController(config.Id, config.Name ?? string.Empty,
                async (mode, ct) =>
                {
                    var requests = batteryControl.GetRequests(mode);
                    if (batteryControl.AuthType == JsonRestAuthType.TokenHeader
                        && (string.IsNullOrEmpty(typedConfig.ApiToken) || string.IsNullOrEmpty(batteryControl.TokenHeaderName)))
                    {
                        throw new InvalidOperationException("An API token is required for battery control");
                    }
                    var httpClient = _httpClientFactory.CreateClient(StaticConstants.HttpClientNameShortTimeout);
                    foreach (var request in requests)
                    {
                        ct.ThrowIfCancellationRequested();
                        var uri = GenericJsonRestTemplateValueSetupService.ResolveUriTemplate(request.UriTemplate, typedConfig);
                        using var httpRequest = new HttpRequestMessage(new HttpMethod(request.Method), uri);
                        GenericJsonRestTemplateValueSetupService.ApplyAuthHeaders(httpRequest, batteryControl.AuthType, typedConfig);
                        if (batteryControl.AuthType == JsonRestAuthType.TokenHeader)
                        {
                            httpRequest.Headers.Add(batteryControl.TokenHeaderName!, typedConfig.ApiToken);
                        }
                        if (!string.IsNullOrEmpty(request.JsonBodyTemplate))
                        {
                            var body = GenericJsonRestTemplateValueSetupService.ResolveUriTemplate(request.JsonBodyTemplate, typedConfig);
                            httpRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");
                        }
                        var response = await httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);
                        if (!response.IsSuccessStatusCode)
                        {
                            var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                            throw new InvalidOperationException(
                                $"Battery mode request {request.Method} {uri} failed with status code {response.StatusCode}: {responseBody}");
                        }
                    }
                },
                batteryControl.RewriteInterval));
        }
        return result;
    }
}
