using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Modbus.Contracts;

public interface IModbusValueExecutionService
{
    Task<byte[]> GetResult(DtoModbusConfiguration modbusConfig, DtoModbusValueResultConfiguration resultConfiguration, bool ignoreBackoff);
    Task<decimal> GetValue(byte[] byteArray, DtoModbusValueResultConfiguration resultConfig);
    string GetBinaryString(byte[] byteArray);
    Task WriteValue(DtoModbusConfiguration modbusConfig, ModbusValueType valueType, int address, decimal value, bool ignoreBackoff);
}
