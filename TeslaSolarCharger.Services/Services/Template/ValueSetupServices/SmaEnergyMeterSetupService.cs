using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Services.Services.ValueRefresh;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Services.Services.Template.ValueSetupServices;

public class SmaEnergyMeterSetupService : IGenericValueHandlingService
{
    private readonly ILogger<SmaEnergyMeterSetupService> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IConstants _constants;
    private readonly HashSet<IAutoRefreshingValue<decimal>> _values = new();
    private readonly object _valuesLock = new();

    private CancellationTokenSource? _cancellationTokenSource;

    private const int EnergyMeterPort = 9522;
    private const string MulticastAddress = "239.12.255.254";

    public SmaEnergyMeterSetupService(
        ILogger<SmaEnergyMeterSetupService> logger,
        IDateTimeProvider dateTimeProvider,
        IConstants constants)
    {
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
        _constants = constants;
    }

    public ConfigurationType ConfigurationType => ConfigurationType.TemplateValue;

    public IReadOnlyDictionary<ValueUsage, List<DtoHistoricValue<decimal>>> GetSolarValues()
    {
        _logger.LogTrace("{method}()", nameof(GetSolarValues));
        var result = new Dictionary<ValueUsage, List<DtoHistoricValue<decimal>>>();
        var valueUsages = new HashSet<ValueUsage>
        {
            ValueUsage.InverterPower,
            ValueUsage.GridPower,
            ValueUsage.HomeBatteryPower,
            ValueUsage.HomeBatterySoc,
        };

        foreach (var energyMeterValues in _values)
        {
            foreach (var (valueKey, resultValue) in energyMeterValues.HistoricValues)
            {
                if (valueKey.ValueUsage == default || !valueUsages.Contains(valueKey.ValueUsage.Value))
                {
                    continue;
                }
                if (!result.ContainsKey(valueKey.ValueUsage.Value))
                {
                    result[valueKey.ValueUsage.Value] = new();
                }
                result[valueKey.ValueUsage.Value].Add(resultValue);
            }
        }
        return result;
    }

    public List<IGenericValue<decimal>> GetSnapshot()
    {
        return new(GetAutoRefreshingSnapshot());
    }

    private List<IAutoRefreshingValue<decimal>> GetAutoRefreshingSnapshot()
    {
        lock (_valuesLock)
        {
            return _values.ToList();
        }
    }

    private void AddRefreshables(IEnumerable<IAutoRefreshingValue<decimal>> autoRefreshingValues)
    {
        lock (_valuesLock)
        {
            foreach (var refreshable in autoRefreshingValues)
            {
                _values.Add(refreshable);
            }
        }
    }

    private void RemoveRefreshables(List<IAutoRefreshingValue<decimal>> autoRefreshingValues)
    {
        lock (_valuesLock)
        {
            foreach (var refreshable in autoRefreshingValues)
            {
                _values.Remove(refreshable);
            }
        }
    }

    public async Task StartListener(int configurationId)
    {
        _cancellationTokenSource = new CancellationTokenSource();

        // Start listening in background task
        _ = Task.Run(() => ReceiveEnergyMeterValues(configurationId, _cancellationTokenSource.Token), _cancellationTokenSource.Token);

        await Task.CompletedTask;
    }

    public void StopListener(int configurationId)
    {
        if (_cancellationTokenSource != default)
        {
            _logger.LogInformation("Stopping listener for configuration {configurationId}", configurationId);
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = default;
        }
    }

    private void ReceiveEnergyMeterValues(int configurationId, CancellationToken cancellationToken)
    {
        if (!IPAddress.TryParse(MulticastAddress, out var ipAddress))
        {
            _logger.LogError("Invalid multicast IP address: {address}", MulticastAddress);
            return;
        }

        var groupEndPoint = new IPEndPoint(ipAddress, EnergyMeterPort);

        using var udpClient = new UdpClient(EnergyMeterPort);
        udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        try
        {
            udpClient.JoinMulticastGroup(ipAddress);
            _logger.LogInformation("Joined multicast group {address}:{port} for configuration {configurationId}",
                MulticastAddress, EnergyMeterPort, configurationId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not join multicast group for configuration {configurationId}", configurationId);
            return;
        }

        using var registration = cancellationToken.Register(() =>
        {
            _logger.LogTrace("Cancellation requested, closing UDP client for configuration {configurationId}", configurationId);
            udpClient.Close();
        });

        var snapshot = GetAutoRefreshingSnapshot();
        var energyMeterValues = snapshot.FirstOrDefault(v =>
            v.SourceValueKey.ConfigurationType == ConfigurationType.TemplateValue && v.SourceValueKey.SourceId == configurationId);
        if (energyMeterValues == null)
        {
            energyMeterValues = new AutoRefreshingValue<decimal>(new(configurationId, ConfigurationType.TemplateValue),
                _constants.SolarHistoricValueCapacity);
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogTrace("Waiting for new energy meter values");
                byte[] byteArray;

                try
                {
                    byteArray = udpClient.Receive(ref groupEndPoint);
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Energy meter listener stopped due to cancellation for configuration {configurationId}", configurationId);
                    break;
                }
                catch (SocketException ex) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Energy meter listener stopped due to cancellation for configuration {configurationId}", configurationId);
                    break;
                }

                // Process the received data - this is like handling an event
                ProcessEnergyMeterData(byteArray, configurationId, energyMeterValues);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving energy meter values for configuration {configurationId}", configurationId);
            }
        }

        _logger.LogInformation("Energy meter listener loop ended for configuration {configurationId}", configurationId);
    }

    private void ProcessEnergyMeterData(byte[] byteArray, int configurationId, IAutoRefreshingValue<decimal> energyMeterValues)
    {
        _logger.LogTrace("New energy meter values received");

        if (byteArray.Length < 600)
        {
            _logger.LogTrace("Current datagram is not a correct energy meter datagram. Waiting for next values");
            return;
        }

        var serialNumber = Convert.ToUInt32(ConvertByteArray(byteArray, 20, 4));
        _logger.LogTrace("Serial number of energy meter is {serialNumber}", serialNumber);

        var relevantValues = byteArray.Skip(28).Take(byteArray.Length - 27).ToArray();
        var obisValues = ConvertArrayToObisDictionary(relevantValues);

        try
        {
            var currentOverage = Convert.ToDecimal(
                obisValues.First(v => v.Id == 2 && v.ValueType == ValueMode.Average).Value / 10.0);
            var currentSupply = Convert.ToDecimal(
                obisValues.First(v => v.Id == 1 && v.ValueType == ValueMode.Average).Value / 10.0);

            var overage = currentOverage - currentSupply;

            _logger.LogDebug("Energy meter values - Overage: {overage}W (Current: {currentOverage}W, Supply: {currentSupply}W)",
                overage, currentOverage, currentSupply);

            
            energyMeterValues.UpdateValue(
                new(ValueUsage.GridPower, null, configurationId),
                _dateTimeProvider.DateTimeOffSetUtcNow(),
                overage);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Could not find required OBIS values in energy meter data");
        }
    }

    private List<ObisValue> ConvertArrayToObisDictionary(byte[] byteArray)
    {
        var obisValues = new List<ObisValue>();
        var currentIndex = 0;

        while (currentIndex < byteArray.Length)
        {
            try
            {
                var currentIdBytes = byteArray.Skip(currentIndex).Take(2).ToArray();
                var currentId = BitConverter.ToUInt16(currentIdBytes.Reverse().ToArray());
                var obisValue = new ObisValue() { Id = currentId, };

                if (currentId > 100)
                {
                    break;
                }

                currentIndex += 2;
                var currentLengthBytes = byteArray.Skip(currentIndex).Take(1).First();
                currentIndex += 2;
                ushort currentLength = currentLengthBytes;
                var currentValueBytes = byteArray.Skip(currentIndex).Take(currentLength).ToArray();
                currentIndex += currentLength;
                ulong currentValue;

                if (currentLength == 4)
                {
                    currentValue = BitConverter.ToUInt32(currentValueBytes.Reverse().ToArray());
                    obisValue.ValueType = ValueMode.Average;
                }
                else
                {
                    currentValue = BitConverter.ToUInt64(currentValueBytes.Reverse().ToArray());
                    obisValue.ValueType = ValueMode.Counter;
                }

                obisValue.Value = currentValue;
                obisValues.Add(obisValue);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Error parsing OBIS value at index {index}", currentIndex);
                break;
            }
        }

        return obisValues;
    }

    private ulong ConvertByteArray(byte[] source, int start, int length)
    {
        var tmp = new byte[length];
        Buffer.BlockCopy(source, start, tmp, 0, length);
        var s = BitConverter.ToString(tmp).Replace("-", "");
        var n = Convert.ToUInt64(s, 16);
        return n;
    }

    private enum ValueMode
    {
        Average,
        Counter,
    }

    private class ObisValue
    {
        public int Id { get; set; }
        public ValueMode ValueType { get; set; }
        public ulong Value { get; set; }
    }
}
