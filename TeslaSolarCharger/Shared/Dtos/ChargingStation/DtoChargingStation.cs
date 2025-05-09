using FluentValidation;

namespace TeslaSolarCharger.Shared.Dtos.ChargingStation;

public class DtoChargingStation
{
    public DtoChargingStation(string chargepointId)
    {
        ChargepointId = chargepointId;
    }

    public int Id { get; set; }
    public string ChargepointId { get; set; }
    public bool? CanSwitchBetween1And3Phases { get; set; }
    public bool AutoSwitchBetween1And3PhasesEnabled { get; set; }
    public int? MaxCurrent { get; set; }
}


public class ChargingStationValidator : AbstractValidator<DtoChargingStation>
{
    public ChargingStationValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.MaxCurrent).GreaterThanOrEqualTo(6);
    }
}
