using System.ComponentModel;
using TeslaSolarCharger.Shared.Attributes;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.IndexRazor.CarValues;

public class DtoCarBaseSettings
{
    public int CarId { get; set; }
    [DisplayName("ChargeMode")]
    public ChargeMode ChargeMode { get; set; }

    [DisplayName("Min SOC")]
    [Postfix("%")]
    public int MinimumStateOfCharge { get; set; }

    [DisplayName("Latest Time to Reach State of Charge")]
    public DateTime LatestTimeToReachStateOfCharge { get; set; }
    public bool IgnoreLatestTimeToReachSocDate { get; set; }
    public bool IgnoreLatestTimeToReachSocDateOnWeekend { get; set; }
}
