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
        var client = await GetConnectedModbusTcpClient(host, port, endianess, connectDelay);
        if (registerType == ModbusRegisterType.HoldingRegister)
        {
            return await client.GetByteArrayFromHoldingRegisters(unitIdentifier, address, length, readTimeout);
        }
        return await client.GetByteArrayFromInputRegisters(unitIdentifier, address, length, readTimeout);
    }

    private async Task<IModbusTcpClient> GetConnectedModbusTcpClient(string host, int port, ModbusEndianess endianess, TimeSpan connectDelay)
    {
        logger.LogTrace("{method}({host}, {port})", nameof(GetConnectedModbusTcpClient), host, port);
        var ipAddress = GetIpAddressFromHost(host);
        var key = CreateModbusTcpClientKey(ipAddress.ToString(), port);
        if(_modbusClients.TryGetValue(key, out var modbusClient))
        {
            if (!modbusClient.IsConnected)
            {
                await ConnectModbusClient(modbusClient, ipAddress, port, endianess, connectDelay);
            }
            return modbusClient;
        }

        var client = serviceProvider.GetRequiredService<IModbusTcpClient>();
        await ConnectModbusClient(client, ipAddress, port, endianess, connectDelay);
        _modbusClients.Add(key, client);
        return client;
    }

    private async Task ConnectModbusClient(IModbusTcpClient modbusClient, IPAddress ipAddress, int port, ModbusEndianess endianess,
        TimeSpan connectDelay)
    {
        modbusClient.Connect(new IPEndPoint(ipAddress, port), endianess);
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
