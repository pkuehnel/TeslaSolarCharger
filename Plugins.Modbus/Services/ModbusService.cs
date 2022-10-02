using Plugins.Modbus.Contracts;

namespace Plugins.Modbus.Services;

public class ModbusService : IModbusService
{
    private readonly ILogger<ModbusService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, IModbusClient> _modbusClients = new();

    public ModbusService(ILogger<ModbusService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<int> ReadInt32Value(byte unitIdentifier, ushort startingAddress, ushort quantity,
        string ipAddressString, int port, int connectDelay, int timeout, int? minimumResult)
    {
        _logger.LogTrace("{method}({unitIdentifier}, {startingAddress}, {quantity}, {ipAddressString}, {port}, " +
                         "{connectDelay}, {timeout}, {minimumResult})",
            nameof(ReadInt32Value), unitIdentifier, startingAddress, quantity, ipAddressString, port, 
            connectDelay, timeout,minimumResult);

        var modbusClient = GetModbusClient(ipAddressString, port);

        try
        {
            var value = await modbusClient.ReadInt32Value(unitIdentifier, startingAddress, quantity, ipAddressString, port,
                connectDelay, timeout, minimumResult).ConfigureAwait(false);
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not get value. Dispose modbus client");
            modbusClient.Dispose();
            _modbusClients.Remove(GetKeyString(ipAddressString, port));
            throw;
        }
        
    }

    public async Task<short> ReadInt16Value(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddressString, int port,
        int connectDelay, int timeout, int? minimumResult)
    {
        _logger.LogTrace("{method}({unitIdentifier}, {startingAddress}, {quantity}, {ipAddressString}, {port}, " +
                         "{connectDelay}, {timeout}, {minimumResult})",
            nameof(ReadInt16Value), unitIdentifier, startingAddress, quantity, ipAddressString, port, 
            connectDelay, timeout, minimumResult);
        var modbusClient = GetModbusClient(ipAddressString, port);

        try
        {
            var value = await modbusClient.ReadInt16Value(unitIdentifier, startingAddress, quantity, ipAddressString, port,
                connectDelay, timeout, minimumResult).ConfigureAwait(false);
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not get value. Dispose modbus client");
            modbusClient.Dispose();
            _modbusClients.Remove(GetKeyString(ipAddressString, port));
            throw;
        }
    }

    public async Task<float> ReadFloatValue(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddressString, int port,
        int connectDelay, int timeout, int? minimumResult)
    {
        _logger.LogTrace("{method}({unitIdentifier}, {startingAddress}, {quantity}, {ipAddressString}, {port}, " +
                         "{connectDelay}, {timeout}, {minimumResult})",
            nameof(ReadInt16Value), unitIdentifier, startingAddress, quantity, ipAddressString, port,
            connectDelay, timeout, minimumResult);
        var modbusClient = GetModbusClient(ipAddressString, port);
        try
        {
            var value = await modbusClient.ReadFloatValue(unitIdentifier, startingAddress, quantity, ipAddressString, port,
                connectDelay, timeout, minimumResult).ConfigureAwait(false);
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not get value. Dispose modbus client");
            modbusClient.Dispose();
            _modbusClients.Remove(GetKeyString(ipAddressString, port));
            throw;
        }
    }

    private IModbusClient GetModbusClient(string ipAddressString, int port)
    {
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
        _logger.LogTrace("{disconnects} of {clients} clients diconnected.", disconnectedClients, _modbusClients.Count);
    }
}
