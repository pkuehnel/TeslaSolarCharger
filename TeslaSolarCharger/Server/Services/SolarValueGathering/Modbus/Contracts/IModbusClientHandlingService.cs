using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Modbus.Contracts;

public interface IModbusClientHandlingService
{
    Task<byte[]> GetByteArray(byte unitIdentifier, string host, int port, ModbusEndianess endianess, TimeSpan connectDelay,
        TimeSpan readTimeout,
        ModbusRegisterType registerType, ushort address, ushort length, bool ignoreBackoff);

    Task RemoveClient(string host, int port);
}
