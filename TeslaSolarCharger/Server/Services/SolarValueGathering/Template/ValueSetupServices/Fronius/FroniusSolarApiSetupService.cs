using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq.Expressions;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Rest.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Fronius;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.ValueSetupServices.Fronius;

/// <summary>
/// Gathers values via the Fronius Solar API V1 (GetPowerFlowRealtimeData). Works for all Fronius inverters with
/// activated Solar API, including GEN24 and hybrid systems with battery.
/// </summary>
public class FroniusSolarApiSetupService : IRefreshableValueSetupService
{
    private readonly ILogger<FroniusSolarApiSetupService> _logger;
    private readonly IConstants _constants;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ITemplateValueConfigurationService _templateValueConfigurationService;
    private readonly IHttpClientFactory _httpClientFactory;

    public FroniusSolarApiSetupService(ILogger<FroniusSolarApiSetupService> logger, IConstants constants,
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
        var templateValueGatherType = TemplateValueGatherType.FroniusSolarApiV1;
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
            var typedConfig = config.Configuration.ToObject<DtoFroniusSolarApiTemplateValueConfiguration>();
            if (typedConfig == default || string.IsNullOrEmpty(typedConfig.Host))
            {
                _logger.LogError("Could not deserialize configuration {gatherType} for ID {id}. Json is: {json}",
                    config.GatherType, config.Id, config.Configuration.ToString(Formatting.None));
                continue;
            }
            var host = typedConfig.Host;
            var refreshable = new DelegateRefreshableValue<decimal>(
                _serviceScopeFactory,
                async ct =>
                {
                    var httpClient = _httpClientFactory.CreateClient(StaticConstants.HttpClientNameShortTimeout);
                    var json = await httpClient
                        .GetStringAsync($"http://{host}/solar_api/v1/GetPowerFlowRealtimeData.fcgi", ct)
                        .ConfigureAwait(false);
                    return new(GetValues(JObject.Parse(json)));
                },
                defaultInterval,
                _constants.SolarHistoricValueCapacity,
                new(config.Id, ConfigurationType.TemplateValue)
            );
            result.Add(refreshable);
        }
        return result;
    }

    /// <summary>
    /// Values of a GetPowerFlowRealtimeData response. Grid, battery and state of charge are only reported when the
    /// inverter has them (null without a smart meter or battery), so an inverter without them only delivers production.
    /// </summary>
    internal static Dictionary<ValueKey, decimal> GetValues(JObject root)
    {
        var site = root["Body"]?["Data"]?["Site"];
        if (site == default)
        {
            throw new InvalidDataException("Response of Fronius Solar API does not contain Body.Data.Site");
        }
        var values = new Dictionary<ValueKey, decimal>
        {
            //P_PV: PV production, null while the inverter does not produce
            { new ValueKey(ValueUsage.InverterPower, null, 1), GetDecimal(site["P_PV"]) ?? 0 },
        };
        //P_Grid: positive = import
        AddIfAvailable(values, new ValueKey(ValueUsage.GridPower, null, 2), -GetDecimal(site["P_Grid"]));
        var soc = GetDecimal(root["Body"]?["Data"]?["Inverters"]?["1"]?["SOC"]);
        var batteryPower = GetDecimal(site["P_Akku"]);
        //An idle battery may report no power while it still reports its state of charge.
        if (soc != default)
        {
            batteryPower ??= 0;
        }
        //P_Akku: positive = discharging
        AddIfAvailable(values, new ValueKey(ValueUsage.HomeBatteryPower, null, 3), -batteryPower);
        AddIfAvailable(values, new ValueKey(ValueUsage.HomeBatterySoc, null, 4), soc);
        return values;
    }

    private static void AddIfAvailable(Dictionary<ValueKey, decimal> values, ValueKey valueKey, decimal? value)
    {
        if (value != default)
        {
            values.Add(valueKey, value.Value);
        }
    }

    private static decimal? GetDecimal(JToken? token)
    {
        if (token == default || token.Type == JTokenType.Null)
        {
            return default;
        }
        return token.Value<decimal>();
    }
}
