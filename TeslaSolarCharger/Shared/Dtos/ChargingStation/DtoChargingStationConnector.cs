using FluentValidation;
using System.ComponentModel;
using TeslaSolarCharger.Shared.Attributes;

namespace TeslaSolarCharger.Shared.Dtos.ChargingStation;

public class DtoChargingStationConnector
{
    public int Id { get; set; }
    public int ChargingStationId { get; set; }
    public int ConnectorId { get; set; }
    [DisplayName("Auto switch between 1 and 3 phases")]
    [HelperText("When enabled the charger can automatically switch between a 1 and 3 phase charge. Note: Most of the chargers do not support this and some cars might get a hardware damage if enabled, so enable with care.")]
    public bool AutoSwitchBetween1And3PhasesEnabled { get; set; }
    public int? MinCurrent { get; set; }
    public int? MaxCurrent { get; set; }
}


public class ChargingStationConnectorValidator : AbstractValidator<DtoChargingStationConnector>
{
    public ChargingStationConnectorValidator()
    {
        RuleFor(x => x.MaxCurrent)
            .NotEmpty();
        RuleFor(x => x.MinCurrent)
            .NotEmpty();
        RuleFor(x => x)
            .Must(config => config.MaxCurrent >= config.MinCurrent)
            .WithMessage("Max Current must be greater than or equal to Min Current.");
    }
}
