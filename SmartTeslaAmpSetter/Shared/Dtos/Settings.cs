using System.Text.Json.Serialization;
using SmartTeslaAmpSetter.Shared.Enums;

namespace SmartTeslaAmpSetter.Shared.Dtos;

public class Settings
{
    private List<Car> _cars = null!;

    public Settings()
    {
        Cars = new List<Car>();
    }

    public List<Car> Cars
    {
        get => _cars;
        set
        {
            _cars = value;
            foreach (var car in _cars)
            {
                car.CarConfiguration.UpdatedSincLastWrite = true;
            }
        }
    }
}

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
    private CarConfiguration _carConfiguration;

    public Car()
    {
        CarState = new CarState();
        CarConfiguration = new CarConfiguration();
    }
    public int Id { get; set; }

    public CarConfiguration CarConfiguration
    {
        get => _carConfiguration;
        set
        {
            _carConfiguration = value;
            _carConfiguration.UpdatedSincLastWrite = true;
        }
    }

    public CarState CarState { get; set;}
}