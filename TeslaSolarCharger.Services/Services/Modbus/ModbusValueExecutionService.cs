using Microsoft.Extensions.Logging;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;

namespace TeslaSolarCharger.Services.Services.Modbus;

public class ModbusValueExecutionService(ILogger<ModbusValueExecutionService> logger,
    IModbusValueConfigurationService modbusValueConfigurationService) : IModbusValueExecutionService
{
    public async Task<string> GetResult(DtoModbusConfiguration modbusConfig, DtoModbusValueResultConfiguration resultConfiguration)
    {
        logger.LogTrace("{method}({modbusConfig})", nameof(GetResult), modbusConfig);
        return string.Empty;
    }

    public decimal GetValue(string responseString, DtoModbusConfiguration resultConfig)
    {
        logger.LogTrace("{method}({responseString}, {resultConfig})", nameof(GetValue), responseString, resultConfig);
        return 0;
    }

    public async Task<List<DtoValueConfigurationOverview>> GetModbusValueOverviews()
    {
        logger.LogTrace("{method}()", nameof(GetModbusValueOverviews));
        var modbusConfigurations = await modbusValueConfigurationService.GetModbusConfigurationByPredicate(x => true).ConfigureAwait(false);
        var results = new List<DtoValueConfigurationOverview>();
        foreach (var modbusConfiguration in modbusConfigurations)
        {
            var overviewElement = new DtoValueConfigurationOverview()
            {
                Id = modbusConfiguration.Id,
                Heading = $"{modbusConfiguration.Host}:{modbusConfiguration.Port}",
            };
            results.Add(overviewElement);
            var resultConfigurations = await modbusValueConfigurationService.GetModbusResultConfigurationsByPredicate(x => x.ModbusConfigurationId == modbusConfiguration.Id).ConfigureAwait(false);
            foreach (var resultConfiguration in resultConfigurations)
            {
                string? resultString;
                try
                {
                    resultString = await GetResult(modbusConfiguration, resultConfiguration).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error getting result for modbus result configuration {id}", resultConfiguration.Id);
                    resultString = null;
                }
                var dtoValueResult = new DtoOverviewValueResult() { Id = resultConfiguration.Id, UsedFor = resultConfiguration.UsedFor, };
                try
                {
                    dtoValueResult.CalculatedValue =
                        resultString == null ? null : GetValue(await GetResult(modbusConfiguration, resultConfiguration), modbusConfiguration);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error getting value for modbus result configuration {id}", modbusConfiguration.Id);
                    dtoValueResult.CalculatedValue = null;
                }
                finally
                {
                    overviewElement.Results.Add(dtoValueResult);
                }
            }
            
        }
        return results;
    }
}
