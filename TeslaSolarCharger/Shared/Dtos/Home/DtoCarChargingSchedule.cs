using FluentValidation;
using Newtonsoft.Json;
using System.Security.Cryptography.X509Certificates;
using TeslaSolarCharger.Shared.Attributes;

namespace TeslaSolarCharger.Shared.Dtos.Home;

public class DtoCarChargingSchedule
{
    public int Id { get; set; }
    public int TargetSoc { get; set; }
    public DateTime? TargetDate { get; set; }
    public TimeSpan? TargetTime { get; set; }

    public bool RepeatOnMondays { get; set; }
    public bool RepeatOnTuesdays { get; set; }
    public bool RepeatOnWednesdays { get; set; }
    public bool RepeatOnThursdays { get; set; }
    public bool RepeatOnFridays { get; set; }
    public bool RepeatOnSaturdays { get; set; }
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
