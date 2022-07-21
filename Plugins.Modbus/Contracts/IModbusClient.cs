namespace Plugins.Modbus.Contracts;

public interface IModbusClient
{
    Task<int> ReadIntegerValue(byte unitIdentifier, ushort startingAddress, ushort quantity,
        string ipAddressString,
        int port, float factor, int connectDelay, int timeout, int? minimumResult);

    void DiconnectIfConnected();
}