using FluentValidation;
using TeslaSolarCharger.Shared.Attributes;

namespace TeslaSolarCharger.Shared.Dtos.ChargingStation;

public class DtoChargingStationConnector
{
    public DtoChargingStationConnector(string name)
    {
        Name = name;
    }

    public int Id { get; set; }
    public string Name { get; set; }
    public bool ShouldBeManaged { get; set; }
    public int ChargingStationId { get; set; }
    public int ConnectorId { get; set; }
    public bool AutoSwitchBetween1And3PhasesEnabled { get; set; }
    [Postfix("s")]
    public int? PhaseSwitchCoolDownTimeSeconds { get; set; }
    [Postfix("A")]
    public int? MinCurrent { get; set; }
    [Postfix("A")]
    public int? SwitchOffAtCurrent { get; set; }
    [Postfix("A")]
    public int? SwitchOnAtCurrent { get; set; }
    [Postfix("A")]
    public int? MaxCurrent { get; set; }
    public int ConnectedPhasesCount { get; set; } = 3;
    public int ChargingPriority { get; set; }
    public HashSet<int> AllowedCars { get; set; } = new();
    public bool AllowGuestCars { get; set; }
}


public class ChargingStationConnectorValidator : AbstractValidator<DtoChargingStationConnector>
{
    public ChargingStationConnectorValidator()
    {
        When(x => x.ShouldBeManaged, () =>
        {
            RuleFor(x => x.MaxCurrent)
                .NotEmpty();
            RuleFor(x => x.MinCurrent)
                .NotEmpty();
            RuleFor(x => x.ConnectedPhasesCount)
                .NotEmpty();
            RuleFor(x => x.SwitchOnAtCurrent)
                .Must((model, current) => current >= model.SwitchOffAtCurrent)
                .WithMessage("Switch On at Current must be at least as high as Switch off at Current.");

            RuleFor(x => x.MaxCurrent)
                .NotEmpty()
                .Must((model, max) => max >= model.MinCurrent)
                .WithMessage("Max Current must be greater than or equal to Min Current.");

            RuleFor(x => x.Name)
                .NotEmpty();
        });
        
    }
}
