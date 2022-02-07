using System.Net;
using System.Net.Sockets;
using Plugins.SmaEnergymeter.Dtos;

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
            var currentValues = udpClient.Receive(ref groupEndPoint);
            _logger.LogDebug("New values received");

            var currentSupply =
                Convert.ToDecimal(ConvertByteArray(currentValues, 32, 4) / 10.0);
            var currentOverage =
                Convert.ToDecimal(ConvertByteArray(currentValues, 52, 4) / 10.0);

            _logger.LogTrace("current supply: {currentSupply}", currentSupply);
            _logger.LogTrace("current overage: {currentOverage}", currentOverage);
            if (currentSupply > 0)
            {
                _sharedValues.LastValues.Add(new PowerValue()
                {
                    Timestamp = DateTime.UtcNow,
                    Power = (int)-currentSupply,
                });
            }
            else
            {
                _sharedValues.LastValues.Add(new PowerValue()
                {
                    Timestamp = DateTime.UtcNow,
                    Power = (int)currentOverage,
                });
            }

            var maxValuesInList = _configuration.GetValue<int>("MaxValuesInLastValuesList");
            if (_sharedValues.LastValues.Count > maxValuesInList)
            {
                _sharedValues.LastValues.RemoveRange(0, _sharedValues.LastValues.Count - maxValuesInList);
            }
        }
    }

    private ulong ConvertByteArray(byte[] source, int start, int length)
    {
        var tmp = new byte[length];
        Buffer.BlockCopy(source, start, tmp, 0, length);
        var s = BitConverter.ToString(tmp).Replace("-", "");
        var n = Convert.ToUInt64(s, 16);
        return n;
    }
}