namespace Plugins.Modbus.Contracts;

public interface IModbusService
{
    int ReadIntegerValue(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddressString, int port,
        float factor, int? minimumResult);
}