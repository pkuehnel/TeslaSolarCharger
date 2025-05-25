using System.ComponentModel;

namespace TeslaSolarCharger.Shared.Dtos.ChargingCost.CostConfigurations;

public class FixedPrice
{
    [DisplayName("From Hour")]
    public int FromHour { get; set; }

    [DisplayName("From Minute")]
    public int FromMinute { get; set; }

    [DisplayName("To Hour")]
    public int ToHour { get; set; }

    [DisplayName("To Minute")]
    public int ToMinute { get; set; }

    [DisplayName("Price")]
    public decimal Value { get; set; }
    public List<DayOfWeek>? ValidOnDays { get; set; }
}
