using TeslaSolarCharger.Shared.Dtos.Contracts;

namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class Settings : ISettings
{
    private List<Car> _cars = null!;

    public Settings()
    {
        Cars = new List<Car>();
    }

    public int? InverterPower { get; set; }
    public int? Overage { get; set; }
    public int? HomeBatterySoc { get; set; }
    public int? HomeBatteryPower { get; set; }

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