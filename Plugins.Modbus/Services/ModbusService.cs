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
        _logger.LogTrace("{method}({unitIdentifier}, {startingAddress}, {quantity}, {ipAddressString}, {port}, {factor}, {minimumResult})", 
            nameof(ReadIntegerValue), unitIdentifier, startingAddress, quantity, ipAddressString, port, factor, minimumResult);

        var tmpArrayPowerComplete = GetRegisterValue(unitIdentifier, startingAddress, quantity, ipAddressString, port);
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

    public string GetRawBytes(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddressString, int port)
    {
        _logger.LogTrace("{method}({unitIdentifier}, {startingAddress}, {quantity}, {ipAddressString}, {port})",
            nameof(ReadIntegerValue), unitIdentifier, startingAddress, quantity, ipAddressString, port);

        var tmpArrayPowerComplete = GetRegisterValue(unitIdentifier, startingAddress, quantity, ipAddressString, port);
        return Convert.ToHexString(tmpArrayPowerComplete);
    }

    private byte[] GetRegisterValue(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddressString,
        int port)
    {
        var ipAddress = IPAddress.Parse(ipAddressString);
        _logger.LogTrace("Connecting Modbus Client...");
        Connect(new IPEndPoint(ipAddress, port));
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