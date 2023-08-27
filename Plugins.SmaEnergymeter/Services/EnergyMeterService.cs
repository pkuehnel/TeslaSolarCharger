using System.Net;
using System.Net.Sockets;
using Plugins.SmaEnergymeter.Dtos;
using Plugins.SmaEnergymeter.Enums;

namespace Plugins.SmaEnergymeter.Services;

public class EnergyMeterService
{
    private readonly ILogger<EnergyMeterService> _logger;
    private readonly IConfiguration _configuration;
    private readonly SharedValues _sharedValues;

    public EnergyMeterService(ILogger<EnergyMeterService> logger, IConfiguration configuration, SharedValues sharedValues)
    {
        _logger = logger;
        _configuration = configuration;
        _sharedValues = sharedValues;
    }

    public void StartLogging()
    {
        _logger.LogTrace("{method}()", nameof(StartLogging));
        var energymeterPort = _configuration.GetValue<int>("EnergyMeterPort");
        _logger.LogDebug("Use energymeterport {engergymeterPort}", energymeterPort);
        using var udpClient = new UdpClient(energymeterPort);
        udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        var ipAddressString = _configuration.GetValue<string>("EnergyMeterMulticastAddress");
        _logger.LogDebug("Use IP Address {ipAddressString}", ipAddressString);
        IPAddress.TryParse(ipAddressString, out var ipAddress);
        _logger.LogDebug("Parsed Ip Adress: {ipAddress}", ipAddress?.ToString());
        var groupEndPoint = new IPEndPoint(ipAddress ?? throw new InvalidOperationException(), energymeterPort);
        _logger.LogDebug("Joining Multicast group");
        try
        {
            udpClient.JoinMulticastGroup(ipAddress);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not join multicast group");
            throw;
        }
        while (true)
        {
            _logger.LogTrace("Waiting for new values");
            var byteArray = udpClient.Receive(ref groupEndPoint);
            _logger.LogTrace("New values received");

            if (byteArray.Length < 600)
            {
                _logger.LogWarning("Current datagram is no correct energymeter datagram. Waiting for next values");
                continue;
            }

            var relevantValues = byteArray.Skip(28).Take(byteArray.Length - 27).ToArray();
            var obisValues = ConvertArrayToObisDictionary(relevantValues);

            var currentOverage =
                Convert.ToDecimal(obisValues.First(v => v.Id == 2 && v.ValueType == ValueMode.Average).Value / 10.0);
            var currentSupply =
                Convert.ToDecimal(obisValues.First(v => v.Id == 1 && v.ValueType == ValueMode.Average).Value / 10.0);
            

            var totalEnergyToGrid = Convert.ToDecimal(obisValues.First(v => v.Id == 2 && v.ValueType == ValueMode.Counter).Value / 3600.0);
            var totalEnergyFromGrid = Convert.ToDecimal(obisValues.First(v => v.Id == 1 && v.ValueType == ValueMode.Counter).Value / 3600.0);

            _sharedValues.PowerFromGridW = (int) currentSupply;
            _sharedValues.PowerToGridW = (int) currentOverage;

            _sharedValues.TotalEnergyToGridWh = totalEnergyToGrid;
            _sharedValues.TotalEnergyFromGridWh = totalEnergyFromGrid;

            _sharedValues.LastValuesFrom = DateTime.UtcNow;

            _logger.LogTrace("current overage: {currentOverage} W", currentOverage);
            _logger.LogTrace("current supply: {currentSupply} W", currentSupply);

            _logger.LogTrace("Total energy to grid: {totalEnergyToGrid} W", totalEnergyToGrid);
            _logger.LogTrace("Total energy from grid: {totalEnergyFromGrid} W", totalEnergyFromGrid);
           
            
            if (currentSupply > 0)
            {
                _sharedValues.OverageW = (int)-currentSupply;
            }
            else
            {
                _sharedValues.OverageW = (int)currentOverage;
            }
        }
    }

    List<ObisValue> ConvertArrayToObisDictionary(byte[] byteArray)
    {
        var obisValues = new List<ObisValue>();
        var currentIndex = 0;

        while (currentIndex < byteArray.Length)
        {
            try
            {
                var currentIdBytes = byteArray.Skip(currentIndex).Take(2).ToArray();
                var currentId = BitConverter.ToUInt16(currentIdBytes.Reverse().ToArray());
                var obisValue = new ObisValue()
                {
                    Id = currentId,
                };
                if(currentId > 100)
                {
                    break;
                }
                currentIndex += 2;
                var currentLenthsBytes = byteArray.Skip(currentIndex).Take(1).First();
                currentIndex+=2;
                ushort currentLength = currentLenthsBytes;
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
                Console.WriteLine(e);
                break;
            }
        }


        return obisValues;
    }
}
