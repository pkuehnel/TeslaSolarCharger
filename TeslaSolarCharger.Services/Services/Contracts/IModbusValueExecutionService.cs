using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;

namespace TeslaSolarCharger.Services.Services.Contracts;

public interface IModbusValueExecutionService
{
    Task<string> GetResult(DtoModbusConfiguration modbusConfig);
    decimal GetValue(string responseString, DtoModbusConfiguration resultConfig);
    Task<List<DtoValueConfigurationOverview>> GetModbusValueOverviews();
}
