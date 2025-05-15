using FluentValidation;

namespace TeslaSolarCharger.Shared.Dtos.ChargingStation;

public class DtoChargingStationConnector
{
    public int Id { get; set; }
    public int ChargingStationId { get; set; }
    public bool AutoSwitchBetween1And3PhasesEnabled { get; set; }
    public int? MinCurrent { get; set; } = 6;
    public int? MaxCurrent { get; set; }
}


public class ChargingStationConnectorValidator : AbstractValidator<DtoChargingStationConnector>
{
    public ChargingStationConnectorValidator()
    {
        RuleFor(x => x.MaxCurrent).GreaterThanOrEqualTo(6);
        RuleFor(x => x)
            .Must(config => config.MaxCurrent >= config.MinCurrent)
            .WithMessage("Max Current must be greater than or equal to Min Current.");
    }
}
