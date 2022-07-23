namespace Plugins.Modbus.Contracts;

public interface IModbusClient
{
    Task<int> ReadInt32Value(byte unitIdentifier, ushort startingAddress, ushort quantity,
        string ipAddressString,
        int port, float factor, int connectDelay, int timeout, int? minimumResult);

    bool DiconnectIfConnected();
    Task<short> ReadInt16Value(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddressString, int port, float factor, int connectDelay, int timeout, int? minimumResult);
}