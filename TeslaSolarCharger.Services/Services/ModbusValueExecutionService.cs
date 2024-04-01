using Microsoft.Extensions.Logging;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;

namespace TeslaSolarCharger.Services.Services;

public class ModbusValueExecutionService(ILogger<ModbusValueExecutionService> logger,
    IModbusValueConfigurationService modbusValueConfigurationService) : IModbusValueExecutionService
{
    public async Task<string> GetResult(DtoModbusConfiguration modbusConfig)
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
            string? resultString;
            var overviewElement = new DtoValueConfigurationOverview()
            {
                Id = modbusConfiguration.Id,
                Heading = $"{modbusConfiguration.Address} ({modbusConfiguration.Host}:{modbusConfiguration.Port})",
            };
            results.Add(overviewElement);
            try
            {
                resultString = await GetResult(modbusConfiguration).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting result for modbus configuration {id}", modbusConfiguration.Id);
                resultString = null;
            }
            var dtoValueResult = new DtoOverviewValueResult() { Id = modbusConfiguration.Id, UsedFor = modbusConfiguration.UsedFor, };
            try
            {
                dtoValueResult.CalculatedValue =
                    resultString == null ? null : GetValue(await GetResult(modbusConfiguration), modbusConfiguration);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting value for modbus configuration {id}", modbusConfiguration.Id);
                dtoValueResult.CalculatedValue = null;
            }
            finally
            {
                overviewElement.Results.Add(dtoValueResult);
            }
        }
        return results;
    }
}
