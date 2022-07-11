namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class Car
{
    private CarConfiguration _carConfiguration;

#pragma warning disable CS8618
    public Car()
#pragma warning restore CS8618
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