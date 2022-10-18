using System.Net;
using FluentModbus;
using Plugins.Modbus.Contracts;

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
         int port, int connectDelay, int timeout)
    {
        var tmpArrayPowerComplete =
            await GetRegisterValue(unitIdentifier, startingAddress, quantity, ipAddressString, port, connectDelay, timeout)
                .ConfigureAwait(false);
        _logger.LogTrace("Reversing Array {array}", Convert.ToHexString(tmpArrayPowerComplete));
        tmpArrayPowerComplete = tmpArrayPowerComplete.Reverse().ToArray();
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
         int port, int connectDelay, int timeout)
    {
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
            var tmpArrayPowerComplete = ReadHoldingRegisters(unitIdentifier, startingAddress, quantity).ToArray();
            return tmpArrayPowerComplete;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not get register value.");
            if (IsConnected)
            {
                _logger.LogDebug("Disconnecting Modcus Client...");
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
}
