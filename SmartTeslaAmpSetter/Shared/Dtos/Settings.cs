using System.Text.Json.Serialization;
using SmartTeslaAmpSetter.Shared.Enums;

namespace SmartTeslaAmpSetter.Shared.Dtos;

public class Settings
{
    public Settings()
    {
        Cars = new();
    }
    public List<Car> Cars { get; set; }
}

public class CarConfiguration
{
    private ChargeMode _chargeMode;
    private int _minimumSoC;
    private DateTime _latestTimeToReachSoC;

    public CarConfiguration()
    {
        UpdatedSincLastWrite = false;
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

public class CarState
{
    public string? Name { get; set; }
    public DateTime ShouldStartChargingSince { get; set; }
    public DateTime ShouldStopChargingSince { get; set; }
    public int SoC { get; set; }
    public int SocLimit { get; set; }
    public string? Geofence { get; set; }
    public TimeSpan TimeUntilFullCharge { get; set; }
    public bool AutoFullSpeedCharge { get; set; }
    public DateTime FullChargeAtMaxAcSpeed
    {
        get
        {
            var socToCharge = (double)SocLimit - SoC;
            if (socToCharge < 0)
            {
                return DateTime.Now + TimeSpan.Zero;
            }

            return DateTime.Now + TimeSpan.FromHours(socToCharge / 15);
        }
    }
    public int LastSetAmp { get; set; }

}

public class Car
{
    public Car()
    {
        CarState = new CarState();
        CarConfiguration = new CarConfiguration();
    }
    public int Id { get; set; }

    public CarConfiguration CarConfiguration { get; set; }
    public CarState CarState { get; set;}
}