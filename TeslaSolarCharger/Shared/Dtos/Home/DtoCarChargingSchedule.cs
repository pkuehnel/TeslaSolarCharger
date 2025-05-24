using FluentValidation;

namespace TeslaSolarCharger.Shared.Dtos.Home;

public class DtoCarChargingSchedule
{
    public int Id { get; set; }
    public int TargetSoc { get; set; }

    public DateTimeOffset? NextOccurrence { get; set; }
    public bool RepeatOnMondays { get; set; }
    public bool RepeatOnTuesdays { get; set; }
    public bool RepeatOnWednesdays { get; set; }
    public bool RepeatOnThursdays { get; set; }
    public bool RepeatOnFridays { get; set; }
    public bool RepeatOnSaturdays { get; set; }
    public bool RepeatOnSundays { get; set; }
}

public class CarChargingScheduleValidator : AbstractValidator<DtoCarChargingSchedule>
{
    public CarChargingScheduleValidator()
    {
        RuleFor(x => x.NextOccurrence)
            .NotEmpty();
    }
}
