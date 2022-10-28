using TeslaSolarCharger.Shared.Enums;

namespace Plugins.Modbus.Contracts;

public interface IModbusService
{
    Task<object> ReadValue<T>(byte unitIdentifier, ushort startingAddress, ushort quantity,
        string ipAddressString, int port, int connectDelay, int timeout, ModbusRegisterType modbusRegisterType) where T : struct;

    Task<string> GetBinaryString(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddress, int port,
        int connectDelaySeconds, int timeoutSeconds, ModbusRegisterType modbusRegisterType);
}
