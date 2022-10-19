using TeslaSolarCharger.Shared.Enums;

namespace Plugins.Modbus.Contracts;

public interface IModbusClient : IDisposable
{
    bool DiconnectIfConnected();
    Task<byte[]> GetByteArray(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddressString,
        int port, int connectDelay, int timeout, ModbusRegisterType modbusRegisterType);
}
