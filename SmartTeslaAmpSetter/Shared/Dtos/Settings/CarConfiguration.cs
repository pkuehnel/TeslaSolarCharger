using System.Text.Json.Serialization;
using SmartTeslaAmpSetter.Shared.Enums;

namespace SmartTeslaAmpSetter.Shared.Dtos.Settings;

public class CarConfiguration
{
    private ChargeMode _chargeMode;
    private int _minimumSoC;
    private DateTime _latestTimeToReachSoC;

    public CarConfiguration()
    {
        UpdatedSincLastWrite = true;
    }

    [JsonIgnore]
    public bool UpdatedSincLastWrite { get; set; }

    public ChargeMode ChargeMode
    {
        get => _chargeMode;
        set
        {
            _chargeMode = value;
            UpdatedSincLastWrite = true;
        }
    }

    public int MinimumSoC
    {
        get => _minimumSoC;
        set
        {
            _minimumSoC = value;
            UpdatedSincLastWrite = true;
        }
    }

    public DateTime LatestTimeToReachSoC
    {
        get => _latestTimeToReachSoC;
        set
        {
            _latestTimeToReachSoC = value;
            UpdatedSincLastWrite = true;
        }
    }
}