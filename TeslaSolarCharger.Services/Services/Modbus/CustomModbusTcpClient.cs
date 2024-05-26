using FluentModbus;
using Microsoft.Extensions.Logging;
using System.Net;
using TeslaSolarCharger.Services.Services.Modbus.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Services.Services.Modbus;

public class CustomModbusTcpClient (ILogger<CustomModbusTcpClient> logger) : ModbusTcpClient, IModbusTcpClient
{
    private readonly SemaphoreSlim _semaphoreSlim = new(1);

    public async Task<byte[]> GetByteArrayFromHoldingRegisters(byte unitIdentifier, ushort startingAddress, ushort quantity,
        TimeSpan readTimeout)
    {
        logger.LogTrace("{method}({unitIdentifier}, {startingAddress}, {quantity})", nameof(GetByteArrayFromHoldingRegisters), unitIdentifier, startingAddress, quantity);
        await _semaphoreSlim.WaitAsync().ConfigureAwait(false);
        try
        {
            ReadTimeout = (int)readTimeout.TotalMilliseconds;
            var result = await base.ReadHoldingRegistersAsync(unitIdentifier, startingAddress, quantity);
            return result.ToArray();
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    public async Task<byte[]> GetByteArrayFromInputRegisters(byte unitIdentifier, ushort startingAddress, ushort quantity,
        TimeSpan readTimeout)
    {
        logger.LogTrace("{method}({unitIdentifier}, {startingAddress}, {quantity})", nameof(GetByteArrayFromInputRegisters), unitIdentifier, startingAddress, quantity);
        await _semaphoreSlim.WaitAsync().ConfigureAwait(false);
        try
        {
            ReadTimeout = (int)readTimeout.TotalMilliseconds;
            var result = await base.ReadInputRegistersAsync(unitIdentifier, startingAddress, quantity);
            return result.ToArray();
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    public void Demo()
    {
        Connect();
    }

    public void Connect(IPEndPoint ipEndPoint, ModbusEndianess endianess, TimeSpan connectTimeout)
    {
        var fluentEndianness = endianess switch
        {
            ModbusEndianess.BigEndian => ModbusEndianness.BigEndian,
            ModbusEndianess.LittleEndian => ModbusEndianness.LittleEndian,
            _ => throw new ArgumentOutOfRangeException(nameof(endianess), endianess, "Endianess not known"),
        };
        ConnectTimeout = (int)connectTimeout.TotalMilliseconds;
        base.Connect(ipEndPoint, fluentEndianness);
    }
}
