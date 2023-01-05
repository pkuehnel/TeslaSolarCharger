using FluentModbus;
using Plugins.Modbus.Contracts;
using System.Net;
using TeslaSolarCharger.Shared.Enums;

namespace Plugins.Modbus.Services;

public class ModbusClient : ModbusTcpClient, IModbusClient
{
    private readonly ILogger<ModbusClient> _logger;
    private readonly SemaphoreSlim _semaphoreSlim;

    public ModbusClient(ILogger<ModbusClient> logger)
    {
        _logger = logger;
        _semaphoreSlim = new SemaphoreSlim(1);
    }

    public async Task<byte[]> GetByteArray(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddressString,
        int port, int connectDelay, int timeout, ModbusRegisterType modbusRegisterType, bool registerSwap)
    {
        _logger.LogTrace("{method}({unitIdentifier}, {startingAddress}, {quantity}, {ipAddress}, {port}, {connectDelay}, {timeout}, {modbusRegisterType})",
            nameof(GetByteArray), unitIdentifier, startingAddress, quantity, ipAddressString, port, connectDelay, timeout, modbusRegisterType);
        var tmpArrayPowerComplete =
            await GetRegisterValue(unitIdentifier, startingAddress, quantity, ipAddressString, port, connectDelay, timeout, modbusRegisterType)
                .ConfigureAwait(false);
        _logger.LogTrace("Reversing Array {array}", Convert.ToHexString(tmpArrayPowerComplete));

        tmpArrayPowerComplete = tmpArrayPowerComplete.Reverse().ToArray();

        if (registerSwap)
            SwapRegisters(tmpArrayPowerComplete);

        return tmpArrayPowerComplete;
    }

    public bool DiconnectIfConnected()
    {
        if (IsConnected)
        {
            _logger.LogDebug("Client disconnected");
            Disconnect();
            return true;
        }

        return false;
    }

    private async Task<byte[]> GetRegisterValue(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddressString,
        int port, int connectDelay, int timeout, ModbusRegisterType modbusRegisterType)
    {
        _logger.LogTrace("{method}({unitIdentifier}, {startingAddress}, {quantity}, {ipAddress}, {port}, {connectDelay}, {timeout}, {modbusRegisterType})",
            nameof(GetRegisterValue), unitIdentifier, startingAddress, quantity, ipAddressString, port, connectDelay, timeout, modbusRegisterType);
        ReadTimeout = (int)TimeSpan.FromSeconds(timeout).TotalMilliseconds;
        WriteTimeout = (int)TimeSpan.FromSeconds(timeout).TotalMilliseconds;
        await _semaphoreSlim.WaitAsync().ConfigureAwait(false);
        if (!IsConnected)
        {
            var ipAddress = IPAddress.Parse(ipAddressString);
            _logger.LogTrace("Connecting Modbus Client...");
            Connect(new IPEndPoint(ipAddress, port));
            await Task.Delay(TimeSpan.FromSeconds(connectDelay)).ConfigureAwait(false);
        }
        _logger.LogTrace("Reading Holding Register...");
        try
        {
            if (modbusRegisterType == ModbusRegisterType.HoldingRegister)
            {
                return ReadHoldingRegisters(unitIdentifier, startingAddress, quantity).ToArray();
            }
            return ReadInputRegisters(unitIdentifier, startingAddress, quantity).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not get register value.");
            if (IsConnected)
            {
                _logger.LogDebug("Disconnecting Modbus Client...");
                Disconnect();
                _logger.LogDebug("Modbus Client disconnected.");
            }

            throw;
        }
        finally
        {
            _logger.LogTrace("Releasing semaphoreSlim...");
#pragma warning disable CS4014
            Task.Run(async () =>
#pragma warning restore CS4014
            {
                await Task.Delay(TimeSpan.FromMilliseconds(Convert.ToInt32(Environment.GetEnvironmentVariable("RequestBlockMilliseconds")))).ConfigureAwait(false);
                _semaphoreSlim.Release();
                _logger.LogTrace("SemaphoreSlim released...");
            });
        }
    }
    private static void SwapRegisters(byte[] data)
    {
        if (data.Length % 4 == 0)
        {
            byte[] _temp = new byte[data.Length];
            for (int i = 0; i < data.Length; i += 4)
            {
                _temp[i + 0] = data[i + 2];
                _temp[i + 1] = data[i + 3];
                _temp[i + 2] = data[i + 0];
                _temp[i + 3] = data[i + 1];
            }
            Buffer.BlockCopy(_temp, 0, data, 0, data.Length);
        }
    }
}
