namespace Plugins.Modbus.Contracts;

public interface IModbusService
{
    int ReadIntegerValue(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddress, int port,
        float factor);
}