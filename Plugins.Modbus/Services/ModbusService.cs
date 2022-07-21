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
        ReadTimeout = 1000;
        WriteTimeout = 1000;
    }
    public void Dispose()
    {
        _logger.LogTrace("{method}()", nameof(Dispose));
        if (IsConnected)
        {
            Disconnect();
        }
    }

    public int ReadIntegerValue(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddressString,
        int port, float factor, int? minimumResult)
    {
        _logger.LogTrace("{method}({unitIdentifier}, {startingAddress}, {quantity}, {ipAddressString}, {port}, {factor})", 
            nameof(ReadIntegerValue), unitIdentifier, startingAddress, quantity, ipAddressString, port, factor);

        var ipAddress = IPAddress.Parse(ipAddressString);
        _logger.LogTrace("Connecting Modbus Client...");
        Connect(new IPEndPoint(ipAddress, port));
        _logger.LogTrace("Reading Holding Register...");
        var tmpArrayPowerComplete = ReadHoldingRegisters(unitIdentifier, startingAddress, quantity).ToArray();
        _logger.LogTrace("Reversing Array...");
        tmpArrayPowerComplete = tmpArrayPowerComplete.Reverse().ToArray();
        _logger.LogTrace("Converting to Int value...");
        var intValue = BitConverter.ToInt32(tmpArrayPowerComplete, 0);
        Disconnect();
        intValue = (int) ((double)factor *  intValue);
        if (minimumResult == null)
        {
            return intValue;
        }
        return intValue < minimumResult ? (int) minimumResult : intValue;
    }
}