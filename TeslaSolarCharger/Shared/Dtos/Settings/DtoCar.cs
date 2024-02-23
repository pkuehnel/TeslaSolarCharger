namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class DtoCar
{
#pragma warning disable CS8618
    public DtoCar()
#pragma warning restore CS8618
    {
        CarState = new CarState();
        CarConfiguration = new CarConfiguration();
    }
    public int Id { get; set; }
    public string Vin { get; set; }

    public CarConfiguration CarConfiguration { get; set; }

    public CarState CarState { get; set;}
}
