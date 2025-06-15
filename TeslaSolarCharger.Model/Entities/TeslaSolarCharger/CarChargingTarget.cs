using TeslaSolarCharger.Model.BaseClasses;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class CarChargingTarget
{
    public int Id { get; set; }
    public int TargetSoc { get; set; }
    public DateOnly? TargetDate { get; set; }
    public TimeOnly TargetTime { get; set; }

    public bool RepeatOnMondays { get; set; }
    public bool RepeatOnTuesdays { get; set; }
    public bool RepeatOnWednesdays { get; set; }
    public bool RepeatOnThursdays { get; set; }
    public bool RepeatOnFridays { get; set; }
    public bool RepeatOnSaturdays { get; set; }
    public bool RepeatOnSundays { get; set; }
    public string? ClientTimeZone { get; set; }

    public int CarId { get; set; }

    public Car Car { get; set; } = null!;
}
