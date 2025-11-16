using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Services.Services.Template.Contracts;
using TeslaSolarCharger.Services.Services.ValueRefresh;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Sma;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Services.Services.Template.ValueSetupServices;

public class SmaEnergyMeterSetupService : IAutoRefreshingValueSetupService
{
    private readonly ILogger<SmaEnergyMeterSetupService> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IConstants _constants;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ITemplateValueConfigurationService _templateValueConfigurationService;

    private const int EnergyMeterPort = 9522;
    private const string MulticastAddress = "239.12.255.254";

    public SmaEnergyMeterSetupService(
        ILogger<SmaEnergyMeterSetupService> logger,
        IDateTimeProvider dateTimeProvider,
        IConstants constants,
        IServiceScopeFactory serviceScopeFactory,
        ITemplateValueConfigurationService templateValueConfigurationService)
    {
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
        _constants = constants;
        _serviceScopeFactory = serviceScopeFactory;
        _templateValueConfigurationService = templateValueConfigurationService;
    }

    public ConfigurationType ConfigurationType => ConfigurationType.TemplateValue;

    public async Task<List<IAutoRefreshingValue<decimal>>> GetDecimalAutoRefreshingValuesAsync(List<int> configurationIds)
    {
        var templateValueGatherType = TemplateValueGatherType.SmaEnergyMeter;
        Expression<Func<TemplateValueConfiguration, bool>> expression = c => c.GatherType == templateValueGatherType && (configurationIds.Count == 0 || configurationIds.Contains(c.Id));
        var templateConfigs = await _templateValueConfigurationService
            .GetConfigurationsByPredicateAsync(expression).ConfigureAwait(false);
        var dtoTemplateValueConfigurationBases = templateConfigs.ToList();
        if (!dtoTemplateValueConfigurationBases.Any())
        {
            return new();
        }
        var autoRefreshingValues = new List<IAutoRefreshingValue<decimal>>();

        //Only one configuration can be used for the SMA Energy Meter as it listens on a fixed multicast address and port
        var relevantConfiguration = dtoTemplateValueConfigurationBases.First();

        var autoRefreshingValue = new AutoRefreshingValue<decimal>(
            _serviceScopeFactory,
            (_, self, ct) =>
            {
                if (!IPAddress.TryParse(MulticastAddress, out var ipAddress))
                {
                    _logger.LogError("Invalid multicast IP address: {address}", MulticastAddress);
                    return Task.CompletedTask;
                }

                var groupEndPoint = new IPEndPoint(ipAddress, EnergyMeterPort);

                using var udpClient = new UdpClient(EnergyMeterPort);
                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                try
                {
                    udpClient.JoinMulticastGroup(ipAddress);
                    _logger.LogInformation("Joined multicast group {address}:{port}",
                        MulticastAddress, EnergyMeterPort);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Could not join multicast group");
                    return Task.CompletedTask;
                }
                uint? serialNumber = null;
                var config = relevantConfiguration.Configuration?.ToObject<DtoSmaEnergyMeterTemplateValueConfiguration>();
                if (config != default)
                {
                    serialNumber = config.SerialNumber;
                }
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        _logger.LogTrace("Waiting for new energy meter values");
                        byte[] byteArray;

                        try
                        {
                            byteArray = udpClient.Receive(ref groupEndPoint);
                        }
                        catch (ObjectDisposedException) when (ct.IsCancellationRequested)
                        {
                            _logger.LogInformation("Energy meter listener stopped due to cancellation");
                            break;
                        }
                        catch (SocketException) when (ct.IsCancellationRequested)
                        {
                            _logger.LogInformation("Energy meter listener stopped due to cancellation");
                            break;
                        }

                        var now = _dateTimeProvider.DateTimeOffSetUtcNow();
                        // Process the received data - this is like handling an event
                        var results = ProcessEnergyMeterData(byteArray, serialNumber);
                        if (results != null)
                        {
                            foreach (var result in results)
                            {
                                self.UpdateValue(result.Key, now, result.Value);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error receiving energy meter values");
                    }
                }

                return Task.CompletedTask;
            },
            _constants.SolarHistoricValueCapacity,
            new SourceValueKey(relevantConfiguration.Id, ConfigurationType.TemplateValue));

        autoRefreshingValues.Add(autoRefreshingValue);
        return autoRefreshingValues;
    }

    private Dictionary<ValueKey, decimal>? ProcessEnergyMeterData(byte[] byteArray, uint? filterForSerialNumber)
    {
        _logger.LogTrace("New energy meter values received");

        if (byteArray.Length < 600)
        {
            _logger.LogTrace("Current datagram is not a correct energy meter datagram. Waiting for next values");
            return null;
        }

        var serialNumber = Convert.ToUInt32(ConvertByteArray(byteArray, 20, 4));
        _logger.LogTrace("Serial number of energy meter is {serialNumber}", serialNumber);
        if(filterForSerialNumber.HasValue && filterForSerialNumber.Value != serialNumber)
        {
            _logger.LogTrace("Serial number {serialNumber} does not match configured serial number {configuredSerialNumber}. Ignoring values.",
                serialNumber, filterForSerialNumber.Value);
            return null;
        }

        var relevantValues = byteArray.Skip(28).Take(byteArray.Length - 27).ToArray();
        var obisValues = ConvertArrayToObisDictionary(relevantValues);
        var values = new Dictionary<ValueKey, decimal>();
        try
        {
            var currentOverage = Convert.ToDecimal(
                obisValues.First(v => v.Id == 2 && v.ValueType == ValueMode.Average).Value / 10.0);
            var currentSupply = Convert.ToDecimal(
                obisValues.First(v => v.Id == 1 && v.ValueType == ValueMode.Average).Value / 10.0);

            var overage = currentOverage - currentSupply;
            values[new ValueKey(ValueUsage.GridPower, null, 0)] = overage;

            _logger.LogTrace("Energy meter values - Overage: {overage}W (Current: {currentOverage}W, Supply: {currentSupply}W)",
                overage, currentOverage, currentSupply);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Could not find required OBIS values in energy meter data");
        }
        return values;
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
