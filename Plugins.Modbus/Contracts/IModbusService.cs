namespace Plugins.Modbus.Contracts;

public interface IModbusService
{
    Task<int> ReadIntegerValue(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddressString,
        int port, float factor, int connectDelay, int timeout, int? minimumResult);
}