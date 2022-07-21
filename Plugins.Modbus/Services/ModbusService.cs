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

    public async Task<int> ReadIntegerValue(byte unitIdentifier, ushort startingAddress, ushort quantity, string ipAddressString, int port,
        float factor, int connectDelay, int timeout, int? minimumResult)
    {
        _logger.LogTrace("{method}({unitIdentifier}, {startingAddress}, {quantity}, {ipAddressString}, {port}, {factor}, {minimumResult})",
            nameof(ReadIntegerValue), unitIdentifier, startingAddress, quantity, ipAddressString, port, factor, minimumResult);
        IModbusClient modbusClient;
        if (_modbusClients.Count < 1)
        {
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
        }
        if (_modbusClients.Any(c => c.Key == ipAddressString))
        {
            _logger.LogDebug("Use exising modbusClient");
            modbusClient = _modbusClients[ipAddressString];
        }
        else
        {
            _logger.LogDebug("Creating new ModbusClient");
            modbusClient = _serviceProvider.GetRequiredService<IModbusClient>();
            _modbusClients.Add(ipAddressString, modbusClient);
        }

        var value = await modbusClient.ReadIntegerValue(unitIdentifier, startingAddress, quantity, ipAddressString, port, factor,
            connectDelay, timeout, minimumResult);
        return value;
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        _logger.LogTrace("Closing all open connections...");
        foreach (var modbusClient in _modbusClients.Values)
        {
            modbusClient.DiconnectIfConnected();
        }
        _logger.LogTrace("All clients diconnected.");
    }
}