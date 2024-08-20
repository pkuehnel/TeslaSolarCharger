﻿using FluentModbus;
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
        using var cts = new CancellationTokenSource((int)readTimeout.TotalMilliseconds);
        try
        {
            ReadTimeout = (int)readTimeout.TotalMilliseconds;
            logger.LogTrace("ReadTimeout: {ReadTimeout}", ReadTimeout);
            var result = await base.ReadHoldingRegistersAsync(unitIdentifier, startingAddress, quantity, cts.Token);
            return result.ToArray();
        }
        catch (OperationCanceledException ex)
        {
            logger.LogWarning(ex, "Read operation for {unitIdentifier}, {startingAddress}, {quantity} was canceled due to timeout. Disconnecting modbus client.", unitIdentifier, startingAddress, quantity);
            Disconnect();
            logger.LogDebug("Modbus Client disconnected.");
            throw;
        }
        finally
        {
            _semaphoreSlim.Release();
            logger.LogTrace("Semaphore released");
        }
    }

    public async Task<byte[]> GetByteArrayFromInputRegisters(byte unitIdentifier, ushort startingAddress, ushort quantity,
        TimeSpan readTimeout)
    {
        logger.LogTrace("{method}({unitIdentifier}, {startingAddress}, {quantity})", nameof(GetByteArrayFromInputRegisters), unitIdentifier, startingAddress, quantity);
        await _semaphoreSlim.WaitAsync().ConfigureAwait(false);
        using var cts = new CancellationTokenSource((int)readTimeout.TotalMilliseconds);
        try
        {
            ReadTimeout = (int)readTimeout.TotalMilliseconds;
            logger.LogTrace("ReadTimeout: {ReadTimeout}", ReadTimeout);
            var result = await base.ReadInputRegistersAsync(unitIdentifier, startingAddress, quantity, cts.Token);
            return result.ToArray();
        }
        catch (OperationCanceledException ex)
        {
            logger.LogWarning(ex, "Read operation for {unitIdentifier}, {startingAddress}, {quantity} was canceled due to timeout. Disconnecting modbus client.", unitIdentifier, startingAddress, quantity);
            Disconnect();
            logger.LogDebug("Modbus Client disconnected.");
            throw;
        }
        finally
        {
            _semaphoreSlim.Release();
            logger.LogTrace("Semaphore released");
        }
    }

    public void Demo()
    {
        Connect();
    }

    public async Task Connect(IPEndPoint ipEndPoint, ModbusEndianess endianess, TimeSpan connectTimeout)
    {
        logger.LogTrace("{method}({ipEndPoint}, {endianess}, {connectTimeout})", nameof(Connect), ipEndPoint, endianess, connectTimeout);
        await _semaphoreSlim.WaitAsync().ConfigureAwait(false);
        try
        {
            var fluentEndianness = endianess switch
            {
                ModbusEndianess.BigEndian => ModbusEndianness.BigEndian,
                ModbusEndianess.LittleEndian => ModbusEndianness.LittleEndian,
                _ => throw new ArgumentOutOfRangeException(nameof(endianess), endianess, "Endianess not known"),
            };
            ConnectTimeout = (int)connectTimeout.TotalMilliseconds;
            logger.LogTrace("ConnectTimeout: {ConnectTimeout}", ConnectTimeout);
            base.Connect(ipEndPoint, ModbusEndianness.BigEndian);
        }
        finally
        {
            _semaphoreSlim.Release();
            logger.LogTrace("Semaphore relesed");
        }

    }
}
