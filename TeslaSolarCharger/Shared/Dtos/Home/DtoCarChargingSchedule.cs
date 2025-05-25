using FluentValidation;
using Newtonsoft.Json;
using System.ComponentModel;

namespace TeslaSolarCharger.Shared.Dtos.Home;

public class DtoCarChargingSchedule
{
    public int Id { get; set; }
    public int TargetSoc { get; set; }
    public DateTime? TargetDate { get; set; }
    public TimeSpan? TargetTime { get; set; }

    [DisplayName("Mo")]
    public bool RepeatOnMondays { get; set; }
    [DisplayName("Tu")]
    public bool RepeatOnTuesdays { get; set; }
    [DisplayName("We")]
    public bool RepeatOnWednesdays { get; set; }
    [DisplayName("Th")]
    public bool RepeatOnThursdays { get; set; }
    [DisplayName("Fr")]
    public bool RepeatOnFridays { get; set; }
    [DisplayName("Sa")]
    public bool RepeatOnSaturdays { get; set; }
    [DisplayName("Su")]
    public bool RepeatOnSundays { get; set; }

    [JsonIgnore]
    public bool RepeatsOnAnyDay {
        get
        {
            return RepeatOnMondays
                   || RepeatOnTuesdays
                   || RepeatOnWednesdays
                   || RepeatOnThursdays
                   || RepeatOnFridays
                   || RepeatOnSaturdays
                   || RepeatOnSundays;
        }

    }
}

public class CarChargingScheduleValidator : AbstractValidator<DtoCarChargingSchedule>
{
    public CarChargingScheduleValidator()
    {
        When(x => !x.RepeatsOnAnyDay, () =>
        {
            RuleFor(x => x)
                .Must(x => x.TargetDate != default)
                .WithMessage("Either a target date or any repetition needs to be set");
        });
        RuleFor(x => x.TargetTime)
            .NotEmpty();
    }
}
