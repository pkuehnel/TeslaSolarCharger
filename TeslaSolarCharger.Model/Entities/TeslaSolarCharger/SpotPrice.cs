namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class SpotPrice
{
    public int Id { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set;}
    public decimal Price { get; set; }
}
