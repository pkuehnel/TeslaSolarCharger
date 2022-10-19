using TeslaSolarCharger.Shared.Enums;

namespace Plugins.Modbus.Contracts;

public interface IModbusService
{
    Task<object> ReadValue<T>(byte unitIdentifier, ushort startingAddress, ushort quantity,
        string ipAddressString, int port, int connectDelay, int timeout, ModbusRegisterType modbusRegisterType) where T : struct;
}
