using System.Text.Json.Serialization;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class CarConfiguration
{
    private ChargeMode _chargeMode;
    private int _minimumSoC;
    private DateTime _latestTimeToReachSoC;
    private int _maximumAmpere;
    private int _minimumAmpere;
    private int _usableEnergy;
    private bool? _shouldBeManaged;
    private int _chargingPriority;
    private bool _ignoreLatestTimeToReachSocDate;

    public CarConfiguration()
    {
        UpdatedSincLastWrite = true;
        _shouldBeManaged = true;
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

    public bool IgnoreLatestTimeToReachSocDate
    {
        get => _ignoreLatestTimeToReachSocDate;
        set
        {
            _ignoreLatestTimeToReachSocDate = value;
            UpdatedSincLastWrite = true;
        }
    }

    public int MaximumAmpere
    {
        get => _maximumAmpere;
        set
        {
            _maximumAmpere = value;
            UpdatedSincLastWrite = true;
        }
    }

    public int MinimumAmpere
    {
        get => _minimumAmpere;
        set
        {
            _minimumAmpere = value;
            UpdatedSincLastWrite = true;
        }
    }

    public int UsableEnergy
    {
        get => _usableEnergy;
        set
        {
            _usableEnergy = value;
            UpdatedSincLastWrite = true;
        }
    }

    public bool? ShouldBeManaged
    {
        get => _shouldBeManaged;
        set
        {
            _shouldBeManaged = value;
            UpdatedSincLastWrite = true;
        }
    }

    public int ChargingPriority
    {
        get => _chargingPriority;
        set
        {
            _chargingPriority = value;
            UpdatedSincLastWrite = true;
        }
    }
}
