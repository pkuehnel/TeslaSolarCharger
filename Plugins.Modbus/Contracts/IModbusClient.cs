namespace Plugins.Modbus.Contracts;

public interface IModbusClient : IDisposable
{
    Task<int> ReadInt32Value(byte unitIdentifier, ushort startingAddress, ushort quantity,
        string ipAddressString,
        int port, int connectDelay, int timeout, int? minimumResult);

    bool DiconnectIfConnected();
    Task<short> ReadInt16Value(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddressString, int port, int connectDelay, int timeout, int? minimumResult);
    Task<float> ReadFloatValue(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddressString, int port, int connectDelay, int timeout, int? minimumResult);
}
