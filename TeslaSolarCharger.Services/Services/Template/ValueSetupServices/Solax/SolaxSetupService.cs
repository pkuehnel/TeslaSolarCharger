using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Linq.Expressions;
using System.Text.Json.Serialization;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Services.Services.Template.Contracts;
using TeslaSolarCharger.Services.Services.ValueRefresh;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Solax;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Services.Services.Template.ValueSetupServices.Solax;

public class SolaxSetupService : IRefreshableValueSetupService
{
    private readonly ILogger<SolaxSetupService> _logger;
    private readonly ITemplateValueConfigurationService _templateValueConfigurationService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConstants _constants;

    public SolaxSetupService(ILogger<SolaxSetupService> logger,
        ITemplateValueConfigurationService templateValueConfigurationService, IServiceScopeFactory serviceScopeFactory,
        IConstants constants)
    {
        _logger = logger;
        _templateValueConfigurationService = templateValueConfigurationService;
        _serviceScopeFactory = serviceScopeFactory;
        _constants = constants;
    }


    public ConfigurationType ConfigurationType => ConfigurationType.TemplateValue;
    public async Task<List<DelegateRefreshableValue<decimal>>> GetDecimalRefreshableValuesAsync(TimeSpan defaultInterval, List<int> configurationIds)
    {
        _logger.LogTrace("{method}({defaultInterval})", nameof(GetDecimalRefreshableValuesAsync), defaultInterval);
        var templateValueGatherType = TemplateValueGatherType.SolaxApi;
        Expression<Func<TemplateValueConfiguration, bool>> expression = c => c.GatherType == templateValueGatherType && (configurationIds.Count == 0 || configurationIds.Contains(c.Id));
        var configs = await _templateValueConfigurationService
            .GetConfigurationsByPredicateAsync(expression).ConfigureAwait(false);

        var result = new List<DelegateRefreshableValue<decimal>>();
        foreach (var config in configs)
        {
            if (config.Configuration == default)
            {
                _logger.LogError("Template configuration with ID {id} has empty configuration", config.Id);
                continue;
            }
            var typedConfig = config.Configuration.ToObject<DtoSolaxTemplateValueConfiguration>();
            if (typedConfig == default)
            {
                _logger.LogError("Could not deserialize configuration {gatherType} for ID {id}. Json is: {json}", config.GatherType, config.Id, config.Configuration.ToString(Formatting.None));
                continue;
            }
            try
            {
                var refreshable = new DelegateRefreshableValue<decimal>(
                    _serviceScopeFactory,
                    async ct =>
                    {
                        using var executionScope = _serviceScopeFactory.CreateScope();
                        var solaxDto = await GetSolaxDto(executionScope, typedConfig, ct);
                        var values = GetPvValuesFromSolaxDto(solaxDto);
                        return new(values);
                    },
                    defaultInterval,
                    _constants.SolarHistoricValueCapacity,
                    new(config.Id, ConfigurationType.TemplateValue)
                );

                result.Add(refreshable);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating refreshable for Solax configuration {id} ({host})", config.Id, typedConfig.Host);
            }
        }
        return result;
    }

    private async Task<DtoSolaxValues> GetSolaxDto(IServiceScope executionScope, DtoSolaxTemplateValueConfiguration typedConfig,
        CancellationToken ct)
    {
        var httpClientFactory = executionScope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var url = "http://" + typedConfig.Host;
        var parameters = new List<KeyValuePair<string, string>>()
        {
            new("optType", "ReadRealTimeData"),
            new ("pwd", typedConfig.Password ?? string.Empty ),
        };
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        var content = new FormUrlEncodedContent(parameters);
        request.Content = content;
        var httpClient = httpClientFactory.CreateClient();
        var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var responseContentString = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            _logger.LogError("Error while sending post to Solax. Response: {response}", responseContentString);
        }
        response.EnsureSuccessStatusCode();
        var serializedString = await response.Content.ReadAsStringAsync(ct);
        var solaxDto = JsonConvert.DeserializeObject<DtoSolaxValues>(serializedString);
        if (solaxDto == null)
        {
            var exception = new InvalidDataException($"Returned data string {serializedString} can not be deserialized.");
            _logger.LogError(exception, "Returned string is {string}", serializedString);
            throw exception;
        }
        return solaxDto;
    }

    private Dictionary<ValueKey, decimal> GetPvValuesFromSolaxDto(DtoSolaxValues solaxDto)
    {
        const int uncalculatedBatteryPowerIndex = 41;
        const int uncalculatedGridPowerIndex = 34;
        const int switchPoint = 32767;
        const int maxPoint = 65535;
        var uncalculatedBatteryPower = solaxDto.Data[uncalculatedBatteryPowerIndex];
        var uncalculatedGridPower = solaxDto.Data[uncalculatedGridPowerIndex];
        var actualBatteryPower = uncalculatedBatteryPower < switchPoint ? uncalculatedBatteryPower : uncalculatedBatteryPower - maxPoint;
        var actualGridPower = uncalculatedGridPower < switchPoint ? uncalculatedGridPower : uncalculatedGridPower - maxPoint;

        const int power1Index = 14;
        const int power2Index = 15;
        const int batterySocIndex = 103;
        var pv1Power = solaxDto.Data[power1Index];
        var pv2Power = solaxDto.Data[power2Index];
        var result = new Dictionary<ValueKey, decimal>
        {
            { new(ValueUsage.InverterPower, null, 1), pv1Power + pv2Power },
            { new(ValueUsage.GridPower, null, 2), actualGridPower },
            { new(ValueUsage.HomeBatteryPower, null, 3), actualBatteryPower },
            { new(ValueUsage.HomeBatterySoc, null, 4), solaxDto.Data[batterySocIndex] },
        };
        return result;
    }


    private class DtoSolaxValues
    {
        [JsonPropertyName("sn")]
#pragma warning disable CS8618
        public string SerialNumber { get; set; }
#pragma warning restore CS8618

        [JsonPropertyName("ver")]
#pragma warning disable CS8618
        public string Version { get; set; }
#pragma warning restore CS8618

        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("Data")]
#pragma warning disable CS8618
        public List<int> Data { get; set; }
#pragma warning restore CS8618

        [JsonPropertyName("Information")]
#pragma warning disable CS8618
        public List<object> Information { get; set; }
#pragma warning restore CS8618
    }
}
