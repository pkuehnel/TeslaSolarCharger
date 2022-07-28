namespace Plugins.Modbus.Contracts;

public interface IModbusService
{
    Task<int> ReadInt32Value(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddressString,
        int port, int connectDelay, int timeout, int? minimumResult);

    Task<short> ReadInt16Value(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddress, int port, int connectDelaySeconds, int timeoutSeconds, int? minimumResult);
}