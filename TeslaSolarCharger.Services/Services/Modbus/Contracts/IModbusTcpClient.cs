using System.Net;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Services.Services.Modbus.Contracts;

public interface IModbusTcpClient : IDisposable
{
    bool IsConnected { get; }
    Task Connect(IPEndPoint ipEndPoint, ModbusEndianess endianess, TimeSpan connectTimeout);
    void Disconnect();
    Task<byte[]> GetByteArrayFromHoldingRegisters(byte unitIdentifier, ushort startingAddress, ushort quantity, TimeSpan readTimeout);
    Task<byte[]> GetByteArrayFromInputRegisters(byte unitIdentifier, ushort startingAddress, ushort quantity, TimeSpan readTimeout);
}
