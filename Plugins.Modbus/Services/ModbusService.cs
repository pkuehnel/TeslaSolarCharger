using Plugins.Modbus.Contracts;
using System.Text;
using TeslaSolarCharger.Shared.Enums;

namespace Plugins.Modbus.Services;

public class ModbusService : IModbusService
{
    private readonly ILogger<ModbusService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, IModbusClient> _modbusClients = new();

    private readonly string _byteDelimiter = " ";

    public ModbusService(ILogger<ModbusService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<object> ReadValue<T>(byte unitIdentifier, ushort startingAddress, ushort quantity,
        string ipAddressString, int port, int connectDelay, int timeout, ModbusRegisterType modbusRegisterType, bool registerSwap) where T : struct
    {
        _logger.LogTrace("{method}<{type}>({unitIdentifier}, {startingAddress}, {quantity}, {ipAddressString}, {port}, " +
                         "{connectDelay}, {timeout}, {modbusRegisterType})",
            nameof(ReadValue), typeof(T), unitIdentifier, startingAddress, quantity, ipAddressString, port,
            connectDelay, timeout, modbusRegisterType);

        var byteArray = await GetByteArray(unitIdentifier, startingAddress, quantity, ipAddressString, port, connectDelay, timeout, modbusRegisterType, registerSwap).ConfigureAwait(false);

        if (typeof(T) == typeof(int))
        {
            return (T)Convert.ChangeType(BitConverter.ToInt32(byteArray, 0), typeof(T));
        }

        if (typeof(T) == typeof(float))
        {
            return (T)Convert.ChangeType(BitConverter.ToSingle(byteArray, 0), typeof(T));
        }

        if (typeof(T) == typeof(short))
        {
            return (T)Convert.ChangeType(BitConverter.ToInt16(byteArray, 0), typeof(T));
        }

        if (typeof(T) == typeof(uint))
        {
            return (T)Convert.ChangeType(BitConverter.ToUInt32(byteArray, 0), typeof(T));
        }

        if (typeof(T) == typeof(ushort))
        {
            return (T)Convert.ChangeType(BitConverter.ToUInt16(byteArray, 0), typeof(T));
        }

        if (typeof(T) == typeof(ulong))
        {
            return (T)Convert.ChangeType(BitConverter.ToUInt64(byteArray, 0), typeof(T));
        }

        throw new NotImplementedException($"Can not convert value of type: {typeof(T)}");

    }

    private async Task<byte[]> GetByteArray(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddressString,
        int port, int connectDelay, int timeout, ModbusRegisterType modbusRegisterType, bool registerSwap)
    {
        _logger.LogTrace("{method}({unitIdentifier}, {startingAddress}, {quantity}, {ipAddressString}, {port}, " +
                         "{connectDelay}, {timeout}, {modbusRegisterType})",
            nameof(ReadValue), unitIdentifier, startingAddress, quantity, ipAddressString, port,
            connectDelay, timeout, modbusRegisterType);
        var modbusClient = GetModbusClient(ipAddressString, port);
        byte[] byteArray;
        if (timeout < 1)
        {
            _logger.LogDebug("Timeout is raised to minimum value of 1 second");
            timeout = 1;
        }

        try
        {
            byteArray = await modbusClient.GetByteArray(unitIdentifier, startingAddress, quantity, ipAddressString, port,
                    connectDelay, timeout, modbusRegisterType, registerSwap)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not get byte array. Dispose modbus client");
            modbusClient.Dispose();
            _modbusClients.Remove(GetKeyString(ipAddressString, port));
            throw;
        }

        return byteArray;
    }

    public async Task<string> GetBinaryString(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddress, int port,
        int connectDelaySeconds, int timeoutSeconds, ModbusRegisterType modbusRegisterType, bool registerSwap)
    {
        var byteArray = await GetByteArray(unitIdentifier, startingAddress, quantity, ipAddress, port, connectDelaySeconds, timeoutSeconds, modbusRegisterType, registerSwap).ConfigureAwait(false);
        byteArray = byteArray.Reverse().ToArray();
        var stringbuilder = new StringBuilder();
        foreach (var byteValue in byteArray)
        {
            stringbuilder.Append(Convert.ToString(byteValue, 2).PadLeft(8, '0'));
            stringbuilder.Append(_byteDelimiter);
        }

        return stringbuilder.ToString();
    }

    public async Task<string> GetBinarySubString(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddress, int port, int connectDelaySeconds,
        int timeoutSeconds, ModbusRegisterType modbusRegisterType, bool registerSwap, int startIndex, int length)
    {
        var binaryString = await GetBinaryString(unitIdentifier, startingAddress, quantity, ipAddress, port, connectDelaySeconds, timeoutSeconds,
            modbusRegisterType, registerSwap).ConfigureAwait(false);
        binaryString = binaryString.Replace(_byteDelimiter, string.Empty);
        return binaryString.Substring(startIndex, length);
    }

    private IModbusClient GetModbusClient(string ipAddressString, int port)
    {
        _logger.LogTrace("{method}({ipAddress}, {port})", nameof(GetModbusClient), ipAddressString, port);
        IModbusClient modbusClient;

        if (_modbusClients.Count < 1)
        {
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }

        var keyString = GetKeyString(ipAddressString, port);
        if (_modbusClients.Any(c => c.Key == keyString))
        {
            _logger.LogDebug("Use exising modbusClient");
            modbusClient = _modbusClients[keyString];
        }
        else
        {
            _logger.LogDebug("Creating new ModbusClient");
            modbusClient = _serviceProvider.GetRequiredService<IModbusClient>();
            _modbusClients.Add(keyString, modbusClient);
        }

        return modbusClient;
    }

    private string GetKeyString(string ipAddressString, int port)
    {
        _logger.LogTrace("{method}({ipAddress}, {port})", nameof(GetKeyString), ipAddressString, port);
        var keyString = $"{ipAddressString}:{port}";
        return keyString;
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        _logger.LogTrace("Closing all open connections...");
        var disconnectedClients = 0;
        foreach (var modbusClient in _modbusClients.Values)
        {
            if (modbusClient.DiconnectIfConnected())
            {
                disconnectedClients++;
            }
        }
        _logger.LogDebug("{disconnects} of {clients} clients diconnected.", disconnectedClients, _modbusClients.Count);
    }
}
