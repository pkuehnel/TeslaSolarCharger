using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.IndexRazor.CarValues;

public class DtoCarBaseSettings
{
    public int CarId { get; set; }
    public ChargeMode ChargeMode { get; set; }
    public int MinimumStateOfCharge { get; set; }
    public DateTime LatestTimeToReachStateOfCharge { get; set; }
    public bool IgnoreLatestTimeToReachSocDate { get; set; }
    public bool IgnoreLatestTimeToReachSocDateOnWeekdays { get; set; }
}
