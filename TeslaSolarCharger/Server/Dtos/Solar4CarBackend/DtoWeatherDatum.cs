namespace TeslaSolarCharger.Server.Dtos.Solar4CarBackend;

public class DtoWeatherDatum
{
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public float SolarRadiationWhPerM2 { get; set; }
}
