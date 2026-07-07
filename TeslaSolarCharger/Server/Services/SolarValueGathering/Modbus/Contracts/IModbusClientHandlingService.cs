using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Modbus.Contracts;

public interface IModbusClientHandlingService
{
    Task<byte[]> GetByteArray(byte unitIdentifier, string host, int port, ModbusEndianess endianess, TimeSpan connectDelay,
        TimeSpan readTimeout,
        ModbusRegisterType registerType, ushort address, ushort length, bool ignoreBackoff);

    /// <summary>
    /// Writes bytes to holding registers. The byte array needs to be in machine order (little endian), the same
    /// order <see cref="GetByteArray"/> returns bytes in. The endianess conversion to wire order is handled internally.
    /// </summary>
    Task WriteByteArray(byte unitIdentifier, string host, int port, ModbusEndianess endianess, TimeSpan connectDelay,
        TimeSpan writeTimeout, ushort address, byte[] valueBytesInMachineOrder, bool ignoreBackoff);

    /// <summary>
    /// Writes a single holding register using modbus function code 6.
    /// </summary>
    Task WriteSingleRegister(byte unitIdentifier, string host, int port, ModbusEndianess endianess, TimeSpan connectDelay,
        TimeSpan writeTimeout, ushort address, ushort value, bool ignoreBackoff);

    Task RemoveClient(string host, int port);
}
