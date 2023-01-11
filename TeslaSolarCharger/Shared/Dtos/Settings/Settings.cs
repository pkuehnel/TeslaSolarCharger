using TeslaSolarCharger.Shared.Dtos.Contracts;

namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class Settings : ISettings
{
    private List<Car> _cars = null!;

    public Settings()
    {
        Cars = new List<Car>();
    }

    public bool IsNewVersionAvailable { get; set; }
    public double? InverterPower { get; set; }
    public double? Overage { get; set; }
    public double? HomeBatterySoc { get; set; }
    public double? HomeBatteryPower { get; set; }
    public List<Issue> ActiveIssues { get; set; } = new();
    public bool ControlledACarAtLastCycle { get; set; }

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
