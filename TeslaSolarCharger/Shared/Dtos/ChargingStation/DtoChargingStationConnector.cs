using FluentValidation;

namespace TeslaSolarCharger.Shared.Dtos.ChargingStation;

public class DtoChargingStationConnector
{
    public int Id { get; set; }
    public int ChargingStationId { get; set; }
    public bool AutoSwitchBetween1And3PhasesEnabled { get; set; }
    public int? MaxCurrent { get; set; }
}


public class ChargingStationConnectorValidator : AbstractValidator<DtoChargingStationConnector>
{
    public ChargingStationConnectorValidator()
    {
        RuleFor(x => x.MaxCurrent).GreaterThanOrEqualTo(6);
    }
}
