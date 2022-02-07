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
        using var udpClient = new UdpClient(energymeterPort);
        var ipAddressString = _configuration.GetValue<string>("EnergyMeterMulticastAddress");
        IPAddress.TryParse(ipAddressString, out var ipAddress);
        var groupEndPoint = new IPEndPoint(ipAddress ?? throw new InvalidOperationException(), energymeterPort);
        udpClient.JoinMulticastGroup(ipAddress);
        while (true)
        {
            var currentValues = udpClient.Receive(ref groupEndPoint);

            var currentSupply =
                Convert.ToDecimal(ConvertByteArray(currentValues, 32, 4) / 10.0);
            var currentOverage =
                Convert.ToDecimal(ConvertByteArray(currentValues, 52, 4) / 10.0);

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