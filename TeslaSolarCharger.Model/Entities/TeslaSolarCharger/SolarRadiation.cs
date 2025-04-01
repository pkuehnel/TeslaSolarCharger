namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class SolarRadiation
{
    public int Id { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public float SolarRadiationWhPerM2 { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
