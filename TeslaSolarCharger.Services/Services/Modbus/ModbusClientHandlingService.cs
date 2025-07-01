using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using TeslaSolarCharger.Services.Services.Modbus.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Services.Services.Modbus;

public class ModbusClientHandlingService (ILogger<ModbusClientHandlingService> logger, IServiceProvider serviceProvider) : IModbusClientHandlingService
{
    private readonly Dictionary<string, IModbusTcpClient> _modbusClients = new();

    public async Task<byte[]> GetByteArray(byte unitIdentifier, string host, int port, ModbusEndianess endianess, TimeSpan connectDelay, TimeSpan readTimeout,
        ModbusRegisterType registerType, ushort address, ushort length)
    {
        logger.LogTrace("{method}({unitIdentifier}, {host}, {port}, {endianess}, {connectDelay}, {readTimeout}, {registerType}, {address}, {length})",
                       nameof(GetByteArray), unitIdentifier, host, port, endianess, connectDelay, readTimeout, registerType, address, length);
        var client = await GetConnectedModbusTcpClient(host, port, endianess, connectDelay, readTimeout);
        byte[] byteArray;
        try
        {
            if (registerType == ModbusRegisterType.HoldingRegister)
            {
                byteArray = await client.GetByteArrayFromHoldingRegisters(unitIdentifier, address, length, readTimeout);
            }
            else
            {
                byteArray = await client.GetByteArrayFromInputRegisters(unitIdentifier, address, length, readTimeout);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while getting byte arry from Modbus TCP client for host {host} and port {port}. Remove client.", host, port);
            RemoveClient(host, port);
            throw;
        }
        return ConvertToCorrectEndianess(endianess, byteArray);
    }

    public void RemoveClient(string host, int port)
    {
        logger.LogTrace("{method}({host}, {port})", nameof(RemoveClient), host, port);
        var key = CreateModbusTcpClientKey(host, port);
        if (_modbusClients.TryGetValue(key, out var client))
        {
            try
            {
                if (client.IsConnected)
                {
                    client.Disconnect();
                }
            }
            catch(Exception ex)
            {
                logger.LogError(ex, "Error while disconnecting Modbus client for host {host} and port {port}.", host, port);
            }

            try
            {
                client.Dispose();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while disposing Modbus client for host {host} and port {port}.", host, port);
            }
            _modbusClients.Remove(key);
        }
    }

    private static byte[] ConvertToCorrectEndianess(ModbusEndianess endianess, byte[] byteArray)
    {
        var tempArray = byteArray.Reverse().ToArray();
        if (endianess == ModbusEndianess.LittleEndian && tempArray.Length % 4 == 0)
        {
            var swappedByteArray = new byte[tempArray.Length];
            for (var i = 0; i < tempArray.Length; i += 4)
            {
                swappedByteArray[i + 0] = tempArray[i + 2];
                swappedByteArray[i + 1] = tempArray[i + 3];
                swappedByteArray[i + 2] = tempArray[i + 0];
                swappedByteArray[i + 3] = tempArray[i + 1];
            }
            return swappedByteArray;
        }
        return tempArray;
    }

    private async Task<IModbusTcpClient> GetConnectedModbusTcpClient(string host, int port, ModbusEndianess endianess,
        TimeSpan connectDelay, TimeSpan connectTimeout)
    {
        logger.LogTrace("{method}({host}, {port})", nameof(GetConnectedModbusTcpClient), host, port);
        var ipAddress = GetIpAddressFromHost(host);
        var key = CreateModbusTcpClientKey(ipAddress.ToString(), port);
        if(_modbusClients.TryGetValue(key, out var modbusClient))
        {
            logger.LogTrace("Found Modbus client, check if connected.");
            if (!modbusClient.IsConnected)
            {
                logger.LogTrace("Modbus client not connected, try to connect.");
                await ConnectModbusClient(modbusClient, ipAddress, port, endianess, connectDelay, connectTimeout);
            }
            return modbusClient;
        }
        logger.LogTrace("Did not find Modbus client, create new one.");
        var client = serviceProvider.GetRequiredService<IModbusTcpClient>();
        _modbusClients.Add(key, client);
        await ConnectModbusClient(client, ipAddress, port, endianess, connectDelay, connectTimeout);
        return client;
    }

    private async Task ConnectModbusClient(IModbusTcpClient modbusClient, IPAddress ipAddress, int port, ModbusEndianess endianess,
        TimeSpan connectDelay, TimeSpan connectTimeout)
    {
        logger.LogTrace("{method}(modbusClient, {ipAddress}, {port}, {endianess}, {connectDelay}, {connectTimeout})", nameof(ConnectModbusClient), ipAddress, port, endianess, connectDelay, connectTimeout);
        await modbusClient.Connect(new IPEndPoint(ipAddress, port), endianess, connectTimeout);
        await Task.Delay(connectDelay).ConfigureAwait(false);
    }

    private IPAddress GetIpAddressFromHost(string host)
    {
        logger.LogTrace("{method}({host})", nameof(GetIpAddressFromHost), host);
        var isIpAddress = IPAddress.TryParse(host, out var ipAddress);
        if (!isIpAddress)
        {
            var hostEntry = Dns.GetHostEntry(host);
            if (hostEntry.AddressList.Length < 1)
            {
                logger.LogError("Could not get IP Address from hostname {host}", host);
                throw new ArgumentException("Could not get IP Address from hostname", nameof(host));
            }
            ipAddress = hostEntry.AddressList[0];
        }
        if (ipAddress != default)
        {
            return ipAddress;
        }

        logger.LogError("Ip Adress from host {host} is null", host);
        throw new ArgumentException("Ip Adress from host is null", nameof(host));

    }

    private string CreateModbusTcpClientKey(string host, int port)
    {
        logger.LogTrace("{method}({host}, {port})", nameof(CreateModbusTcpClientKey), host, port);
        return $"{host}:{port}";
    }
}
