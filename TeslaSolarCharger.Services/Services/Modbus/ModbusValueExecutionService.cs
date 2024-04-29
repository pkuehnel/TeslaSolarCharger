using Microsoft.Extensions.Logging;
using TeslaSolarCharger.Services.Services.Modbus.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;

namespace TeslaSolarCharger.Services.Services.Modbus;

public class ModbusValueExecutionService(ILogger<ModbusValueExecutionService> logger,
    IModbusValueConfigurationService modbusValueConfigurationService, IModbusClientHandlingService modbusClientHandlingService) : IModbusValueExecutionService
{
    public async Task<byte[]> GetResult(DtoModbusConfiguration modbusConfig, DtoModbusValueResultConfiguration resultConfiguration)
    {
        logger.LogTrace("{method}({modbusConfig})", nameof(GetResult), modbusConfig);
        var byteArray = await modbusClientHandlingService.GetByteArray((byte)modbusConfig.UnitIdentifier!, modbusConfig.Host,
            modbusConfig.Port, modbusConfig.Endianess, TimeSpan.FromSeconds(modbusConfig.ConnectDelayMilliseconds),
            TimeSpan.FromMilliseconds(modbusConfig.ReadTimeoutMilliseconds), resultConfiguration.RegisterType,
            (ushort)resultConfiguration.Address, (ushort)resultConfiguration.Length);
        return byteArray;
    }

    public decimal GetValue(byte[] registerResult, DtoModbusConfiguration resultConfig)
    {
        logger.LogTrace("{method}({responseString}, {resultConfig})", nameof(GetValue), registerResult, resultConfig);
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
                var dtoValueResult = new DtoOverviewValueResult() { Id = resultConfiguration.Id, UsedFor = resultConfiguration.UsedFor, };
                try
                {
                    dtoValueResult.CalculatedValue = GetValue(await GetResult(modbusConfiguration, resultConfiguration), modbusConfiguration);
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
