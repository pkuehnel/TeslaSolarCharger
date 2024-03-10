namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class ChargingProcess
{
    public int Id { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? UsedGridEnergy { get; set; }
    public decimal? UsedSolarEnergy { get; set; }
    public decimal? Cost { get; set; }

    public int CarId { get; set; }

    public Car Car { get; set; } = null!;

    public List<ChargingDetail> ChargingDetails { get; set; }
}
