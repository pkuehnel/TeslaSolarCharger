using Microsoft.Extensions.Logging;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Services.Services.Modbus.Contracts;
using TeslaSolarCharger.Services.Services.Mqtt.Contracts;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

namespace TeslaSolarCharger.Services.Services.Rest;

public class ValueOverviewService(
    ILogger<ValueOverviewService> logger,
    IRestValueConfigurationService restValueConfigurationService,
    IMqttConfigurationService mqttConfigurationService,
    IModbusValueConfigurationService modbusValueConfigurationService,
    IGenericValueService genericValueService) : IValueOverviewService
{
    public async Task<List<DtoValueConfigurationOverview>> GetRestValueOverviews()
    {
        logger.LogTrace("{method}()", nameof(GetRestValueOverviews));
        var restValueConfigurations = await restValueConfigurationService
            .GetFullRestValueConfigurationsByPredicate(c => true)
            .ConfigureAwait(false);
        var configurationType = ConfigurationType.RestSolarValue;
        var values = genericValueService.GetAllByPredicate(v =>
            v.SourceValueKey.ConfigurationType == configurationType);
        var overviews = new List<DtoValueConfigurationOverview>();
        foreach (var dtoFullRestValueConfiguration in restValueConfigurations)
        {
            var resultConfigurations = await restValueConfigurationService
                .GetRestResultConfigurationByPredicate(c =>
                    c.RestValueConfigurationId == dtoFullRestValueConfiguration.Id)
                .ConfigureAwait(false);
            var overviewValue = new DtoValueConfigurationOverview(dtoFullRestValueConfiguration.Url)
            {
                Id = dtoFullRestValueConfiguration.Id,
            };
            overviews.Add(overviewValue);
            foreach (var resultConfiguration in resultConfigurations)
            {
                var overviewValueResult = new DtoOverviewValueResult
                {
                    Id = resultConfiguration.Id,
                    UsedFor = resultConfiguration.UsedFor,
                };
                AddResult(values, dtoFullRestValueConfiguration.Id, configurationType, resultConfiguration.Id, overviewValueResult, overviewValue);
            }
        }

        return overviews;
    }

    public async Task<List<DtoValueConfigurationOverview>> GetMqttValueOverviews()
    {
        logger.LogTrace("{method}()", nameof(GetMqttValueOverviews));
        var overviews = new List<DtoValueConfigurationOverview>();
        var configurationType = ConfigurationType.MqttSolarValue;
        var values = genericValueService.GetAllByPredicate(
            v => v.SourceValueKey.ConfigurationType == configurationType);
        var mqttConfigurations = await mqttConfigurationService.GetMqttConfigurationsByPredicate(x => true);
        foreach (var mqttConfiguration in mqttConfigurations)
        {
            var headline = "mqtt://";
            if (!string.IsNullOrWhiteSpace(mqttConfiguration.Username))
            {
                headline += mqttConfiguration.Username + "@";
            }
            headline += mqttConfiguration.Host + ":" + mqttConfiguration.Port;
            var overviewValue = new DtoValueConfigurationOverview(headline)
            {
                Id = mqttConfiguration.Id,
            };
            overviews.Add(overviewValue);

            var resultConfigurations =
                await mqttConfigurationService.GetMqttResultConfigurationsByPredicate(x => x.MqttConfigurationId == mqttConfiguration.Id);
            foreach (var resultConfiguration in resultConfigurations)
            {
                var overviewValueResult = new DtoOverviewValueResult
                {
                    Id = resultConfiguration.Id,
                    UsedFor = resultConfiguration.UsedFor,
                };
                AddResult(values, mqttConfiguration.Id, configurationType, resultConfiguration.Id, overviewValueResult, overviewValue);
            }
        }
        return overviews;
    }

    public async Task<List<DtoValueConfigurationOverview>> GetModbusValueOverviews()
    {
        logger.LogTrace("{method}()", nameof(GetModbusValueOverviews));
        var overviews = new List<DtoValueConfigurationOverview>();
        var configurationType = ConfigurationType.ModbusSolarValue;
        var values = genericValueService.GetAllByPredicate(
            v => v.SourceValueKey.ConfigurationType == configurationType);
        var configurations = await modbusValueConfigurationService.GetModbusConfigurationByPredicate(x => true);
        foreach (var configuration in configurations)
        {
            var headline = configuration.Host + ":" + configuration.Port;
            var overviewValue = new DtoValueConfigurationOverview(headline)
            {
                Id = configuration.Id,
            };
            overviews.Add(overviewValue);

            var resultConfigurations =
                await modbusValueConfigurationService.GetModbusResultConfigurationsByPredicate(x => x.ModbusConfigurationId == configuration.Id);
            foreach (var resultConfiguration in resultConfigurations)
            {
                var overviewValueResult = new DtoOverviewValueResult
                {
                    Id = resultConfiguration.Id,
                    UsedFor = resultConfiguration.UsedFor,
                };
                AddResult(values, configuration.Id, configurationType, resultConfiguration.Id, overviewValueResult, overviewValue);
            }
        }
        return overviews;
    }

    private void AddResult(List<IGenericValue<decimal>> values, int configurationId, ConfigurationType configurationType,
        int resultConfigurationId, DtoOverviewValueResult overviewValueResult,
        DtoValueConfigurationOverview valueOverview)
    {
        var genericValues = values
            .Where(v => v.SourceValueKey == new SourceValueKey(
                configurationId,
                configurationType))
            .ToList();
        var calculatedValue = 0m;
        DateTimeOffset? lastUpdated = default;
        foreach (var genericValue in genericValues)
        {
            foreach (var genericValueHistoricValue in genericValue.HistoricValues)
            {
                if (genericValueHistoricValue.Key.ResultConfigurationId == resultConfigurationId)
                {
                    calculatedValue += genericValueHistoricValue.Value.Value;
                    var timestamp = genericValueHistoricValue.Value.Timestamp;
                    if (lastUpdated == default || lastUpdated < timestamp)
                    {
                        lastUpdated = timestamp;
                    }
                }
            }
        }

        overviewValueResult.CalculatedValue = calculatedValue;
        overviewValueResult.LastRefreshed = lastUpdated ?? default;
        valueOverview.Results.Add(overviewValueResult);
    }
}
