using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;

namespace TeslaSolarCharger.Services.Services.Modbus.Contracts;

public interface IModbusValueExecutionService
{
    Task<byte[]> GetResult(DtoModbusConfiguration modbusConfig, DtoModbusValueResultConfiguration resultConfiguration);
    decimal GetValue(byte[] registerResult, DtoModbusConfiguration resultConfig);
    Task<List<DtoValueConfigurationOverview>> GetModbusValueOverviews();
}
