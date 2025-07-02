using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using TeslaSolarCharger.Services.Services.Modbus.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Services.Services.Modbus;

public class ModbusClientHandlingService (ILogger<ModbusClientHandlingService> logger, IServiceProvider serviceProvider) : IModbusClientHandlingService, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, (IModbusTcpClient client, RetryInfo? retryInfo, SemaphoreSlim semaphoreSlim)> _modbusClients = new();

    private readonly TimeSpan _initialBackoff = TimeSpan.FromSeconds(16);
    private readonly TimeSpan _maxBackoffDuration = TimeSpan.FromHours(5);


    private class RetryInfo
    {
        public int RetryCount { get; set; }
        public DateTime LastAttemptTime { get; set; }
        public TimeSpan NextBackoffDelay { get; set; }
    }

    public async Task<byte[]> GetByteArray(byte unitIdentifier, string host, int port, ModbusEndianess endianess, TimeSpan connectDelay, TimeSpan readTimeout,
        ModbusRegisterType registerType, ushort address, ushort length)
    {
        logger.LogTrace("{method}({unitIdentifier}, {host}, {port}, {endianess}, {connectDelay}, {readTimeout}, {registerType}, {address}, {length})",
                       nameof(GetByteArray), unitIdentifier, host, port, endianess, connectDelay, readTimeout, registerType, address, length);
        EnsureNoBackoffRequired(host, port);
        IModbusTcpClient client;
        try
        {
            client = await GetConnectedModbusTcpClient(host, port, endianess, connectDelay, readTimeout);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get connected Modbus client for host {host} and port {port}", host, port);
            IncrementRetryCount(host, port);
            throw;
        }
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
            ResetRetryCount(host, port);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while getting byte array from Modbus TCP client for host {host} and port {port}. Remove client.", host, port);
            await RemoveClient(host, port);
            IncrementRetryCount(host, port);
            throw;
        }
        return ConvertToCorrectEndianess(endianess, byteArray);
    }

    public async Task RemoveClient(string host, int port)
    {
        logger.LogTrace("{method}({host}, {port})", nameof(RemoveClient), host, port);
        var key = CreateModbusTcpClientKey(host, port);
        await RemoveClientByKey(key).ConfigureAwait(false);
    }

    private async Task RemoveClientByKey(string key)
    {
        logger.LogTrace("{method}({key})", nameof(RemoveClientByKey), key);
        if (_modbusClients.TryGetValue(key, out var element))
        {
            await element.semaphoreSlim.WaitAsync();
            try
            {
                try
                {
                    element.client.Dispose();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while disposing Modbus client for key {key}.", key);
                }
                _modbusClients.Remove(key, out _);
            }
            finally
            {
                element.semaphoreSlim.Release();
                element.semaphoreSlim.Dispose();
            }
        }
    }

    private void EnsureNoBackoffRequired(string host, int port)
    {
        logger.LogTrace("{method}({host}, {port})", nameof(EnsureNoBackoffRequired), host, port);
        var key = CreateModbusTcpClientKey(host, port);
        if (!_modbusClients.TryGetValue(key, out var element) || element.retryInfo == default)
        {
            return; // No previous failures, no need to wait
        }
        var timeSinceLastAttempt = DateTime.UtcNow - element.retryInfo.LastAttemptTime;
        var remainingWaitTime = element.retryInfo.NextBackoffDelay - timeSinceLastAttempt;

        if (remainingWaitTime > TimeSpan.Zero)
        {
            logger.LogError("No connections allowed to Modbus device {host}:{port} for {waitSeconds} seconds (retry attempt {retryCount})",
                host, port, remainingWaitTime.TotalSeconds, element.retryInfo.RetryCount);
            throw new InvalidOperationException($"No connections allowed to Modbus device {host}:{port} for {remainingWaitTime.TotalSeconds} seconds (retry attempt {element.retryInfo.RetryCount})");
        }
    }

    private void IncrementRetryCount(string host, int port)
    {
        logger.LogTrace("{method}({host}, {port})", nameof(IncrementRetryCount), host, port);
        var key = CreateModbusTcpClientKey(host, port);
        if (!_modbusClients.TryGetValue(key, out var element))
        {
            logger.LogWarning("Failed to increment retry count: No Modbus client found for key {key}.", key);
            return;
        }

        element.retryInfo ??= new RetryInfo()
        {
            RetryCount = 0,
            NextBackoffDelay = _initialBackoff,
        };
        _modbusClients[key] = (element.client, element.retryInfo, element.semaphoreSlim);

        element.retryInfo.RetryCount++;
        element.retryInfo.LastAttemptTime = DateTime.UtcNow;

        // Calculate next backoff delay with exponential increase
        var nextDelaySeconds = Math.Min(
            _initialBackoff.TotalSeconds * Math.Pow(2, element.retryInfo.RetryCount - 1),
            _maxBackoffDuration.TotalSeconds
        );
        element.retryInfo.NextBackoffDelay = TimeSpan.FromSeconds(nextDelaySeconds);

        logger.LogWarning("Incremented retry count for {host}:{port} to {retryCount}. Next backoff delay: {nextDelay} seconds",
            host, port, element.retryInfo.RetryCount, nextDelaySeconds);
    }

    private void ResetRetryCount(string host, int port)
    {
        logger.LogTrace("{method}({host}, {port})", nameof(ResetRetryCount), host, port);
        var key = CreateModbusTcpClientKey(host, port);
        if (_modbusClients.TryGetValue(key, out var element) && element.retryInfo != null)
        {
            _modbusClients[key] = (element.client, null, element.semaphoreSlim);
            logger.LogInformation("Reset retry count for {host}:{port} after successful operation", host, port);
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
        var key = CreateModbusTcpClientKey(host, port);

        if (_modbusClients.TryGetValue(key, out var element))
        {
            logger.LogTrace("Found Modbus client, check if connected.");
            await element.semaphoreSlim.WaitAsync();
            try
            {
                if (!element.client.IsConnected)
                {
                    logger.LogTrace("Modbus client not connected, try to connect.");
                    await ConnectModbusClient(element.client, ipAddress, port, endianess, connectDelay, connectTimeout);
                }

                return element.client;
            }
            finally
            {
                element.semaphoreSlim.Release();
            }

        }
        logger.LogTrace("Did not find Modbus client, create new one.");
        var client = serviceProvider.GetRequiredService<IModbusTcpClient>();
        var semaphoreSlim = new SemaphoreSlim(1, 1);
        if (!_modbusClients.TryAdd(key, (client, null, semaphoreSlim)))
        {
            throw new InvalidOperationException($"Looks like a modbus client with key {key} has been added in the meantime.");
        }
        
        await semaphoreSlim.WaitAsync().ConfigureAwait(false);
        try
        {
            await ConnectModbusClient(client, ipAddress, port, endianess, connectDelay, connectTimeout);
        }
        finally
        {
            semaphoreSlim.Release();
        }
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

    public async ValueTask DisposeAsync()
    {
        logger.LogTrace("{method}()", nameof(DisposeAsync));
        var keysToRemove = _modbusClients.Keys.ToList();
        foreach (var key in keysToRemove)
        {
            await RemoveClientByKey(key);
        }
    }
}
