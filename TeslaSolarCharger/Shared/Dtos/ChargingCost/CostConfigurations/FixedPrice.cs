namespace TeslaSolarCharger.Shared.Dtos.ChargingCost.CostConfigurations;

public class FixedPrice
{
    public int FromHour { get; set; }
    public int FromMinute { get; set; }
    public int ToHour { get; set; }
    public int ToMinute { get; set; }
    public decimal Value { get; set; }
    public List<DayOfWeek>? ValidOnDays { get; set; }
}
