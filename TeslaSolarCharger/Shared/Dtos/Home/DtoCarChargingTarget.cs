using FluentValidation;
using Newtonsoft.Json;
using System.ComponentModel;
using TeslaSolarCharger.Shared.Attributes;

namespace TeslaSolarCharger.Shared.Dtos.Home;

public class DtoCarChargingTarget
{
    public int Id { get; set; }
    public int? TargetSoc { get; set; }
    [HelperText("If no Target soc is set, TSC tries to discharge the home battery to its minimum SoC by the target time. If a Target Soc is set, TSC schedules charging to reduce grid usage by reducing the charging speed which your home battery is capable of.")]
    public bool DischargeHomeBatteryToMinSoc { get; set; }
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
    public string? ClientTimeZone { get; set; }
}

public class CarChargingTargetValidator : AbstractValidator<DtoCarChargingTarget>
{
    public CarChargingTargetValidator()
    {
        When(x => !(x.RepeatOnMondays
                    || x.RepeatOnTuesdays
                    || x.RepeatOnWednesdays
                    || x.RepeatOnThursdays
                    || x.RepeatOnFridays
                    || x.RepeatOnSaturdays
                    || x.RepeatOnSundays), () =>
        {
            RuleFor(x => x)
                .Must(x => x.TargetDate != default)
                .WithMessage("Either a target date or any repetition needs to be set");
        });
        When(x => x.TargetSoc.HasValue, () =>
        {
            RuleFor(x => x.TargetSoc)
                .InclusiveBetween(1, 100);
        });
        When(x => !x.TargetSoc.HasValue, () =>
        {
            RuleFor(x => x)
                .Must(x => x.DischargeHomeBatteryToMinSoc)
                .WithMessage("If no target SoC is set, DischargeHomeBatteryToMinSoc must be true");
        });
        When(x => x.TargetDate.HasValue, () =>
        {
            RuleFor(x => x.TargetDate)
                .GreaterThanOrEqualTo(DateTime.Today)
                .WithMessage("If a target date is set, it must be today or in the future");
        });
        RuleFor(x => x.TargetTime)
            .NotEmpty();
    }
}
