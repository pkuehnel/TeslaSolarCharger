using System.Net;
using FluentModbus;
using Plugins.Modbus.Contracts;

namespace Plugins.Modbus.Services;

public class ModbusClient : ModbusTcpClient, IModbusClient
{
    private readonly ILogger<ModbusClient> _logger;

    public ModbusClient(ILogger<ModbusClient> logger)
    {
        _logger = logger;
    }

    public async Task<int> ReadIntegerValue(byte unitIdentifier, ushort startingAddress, ushort quantity,
        string ipAddressString,
        int port, float factor, int connectDelay, int timeout, int? minimumResult)
    {
        _logger.LogTrace("{method}({unitIdentifier}, {startingAddress}, {quantity}, {ipAddressString}, {port}, {factor}, {minimumResult})", 
            nameof(ReadIntegerValue), unitIdentifier, startingAddress, quantity, ipAddressString, port, factor, minimumResult);

        var tmpArrayPowerComplete = await GetRegisterValue(unitIdentifier, startingAddress, quantity, ipAddressString, port, connectDelay, timeout).ConfigureAwait(false);
        _logger.LogTrace("Reversing Array {array}", Convert.ToHexString(tmpArrayPowerComplete));
        tmpArrayPowerComplete = tmpArrayPowerComplete.Reverse().ToArray();
        _logger.LogTrace("Converting {array} to Int value...", Convert.ToHexString(tmpArrayPowerComplete));
        var intValue = BitConverter.ToInt32(tmpArrayPowerComplete, 0);
        intValue = (int) ((double)factor *  intValue);
        if (minimumResult == null)
        {
            return intValue;
        }
        return intValue < minimumResult ? (int) minimumResult : intValue;
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
        catch (Exception)
        {
            if(IsConnected)
            {
                Disconnect();
            }

            throw;
        }
    }
}