namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class Car
{
#pragma warning disable CS8618
    public Car()
#pragma warning restore CS8618
    {
        CarState = new CarState();
        CarConfiguration = new CarConfiguration();
    }
    public int Id { get; set; }

    public CarConfiguration CarConfiguration { get; set; }

    public CarState CarState { get; set;}
}
