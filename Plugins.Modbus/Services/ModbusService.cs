using System.Net;
using FluentModbus;
using Plugins.Modbus.Contracts;

namespace Plugins.Modbus.Services;

public class ModbusService : ModbusTcpClient, IDisposable, IModbusService
{
    private readonly ILogger<ModbusService> _logger;

    public ModbusService(ILogger<ModbusService> logger)
    {
        _logger = logger;
    }
    public void Dispose()
    {
        _logger.LogTrace("{method}()", nameof(Dispose));
        if (IsConnected)
        {
            Disconnect();
        }
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
        Disconnect();
        intValue = (int) ((double)factor *  intValue);
        if (minimumResult == null)
        {
            return intValue;
        }
        return intValue < minimumResult ? (int) minimumResult : intValue;
    }

    public async Task<string> GetRawBytes(byte unitIdentifier, ushort startingAddress, ushort quantity,
        string ipAddressString, int port, int connectDelay, int timeout)
    {
        _logger.LogTrace("{method}({unitIdentifier}, {startingAddress}, {quantity}, {ipAddressString}, {port})",
            nameof(ReadIntegerValue), unitIdentifier, startingAddress, quantity, ipAddressString, port);

        var tmpArrayPowerComplete = await GetRegisterValue(unitIdentifier, startingAddress, quantity, ipAddressString, port, connectDelay, timeout).ConfigureAwait(false);
        return Convert.ToHexString(tmpArrayPowerComplete);
    }

    private async Task<byte[]> GetRegisterValue(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddressString,
        int port, int connectDelay, int timeout)
    {
        ReadTimeout = (int)TimeSpan.FromSeconds(timeout).TotalMilliseconds;
        WriteTimeout = (int)TimeSpan.FromSeconds(timeout).TotalMilliseconds;
        var ipAddress = IPAddress.Parse(ipAddressString);
        _logger.LogTrace("Connecting Modbus Client...");
        Connect(new IPEndPoint(ipAddress, port));
        await Task.Delay(TimeSpan.FromSeconds(connectDelay)).ConfigureAwait(false);
        _logger.LogTrace("Reading Holding Register...");
        try
        {
            var tmpArrayPowerComplete = ReadHoldingRegisters(unitIdentifier, startingAddress, quantity).ToArray();
            return tmpArrayPowerComplete;
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            if (IsConnected)
            {
                Disconnect();
            }
        }
        
    }
}