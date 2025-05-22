using FluentValidation;
using System.ComponentModel;
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
    public int ChargingStationId { get; set; }
    public int ConnectorId { get; set; }
    [DisplayName("Auto switch between 1 and 3 phases")]
    [HelperText("When enabled the charger can automatically switch between a 1 and 3 phase charge. Note: Most of the chargers do not support this and some cars might get a hardware damage if enabled, so enable with care.")]
    public bool AutoSwitchBetween1And3PhasesEnabled { get; set; }
    public int? MinCurrent { get; set; }
    public int? MaxCurrent { get; set; }

    [HelperText("Number of connected phases on your charging station. If you do not know the number, enter 3. Note: Do not enter the number of phases the car can handle, just the number of phases the charging connector is connected to!")]
    public int ConnectedPhasesCount { get; set; } = 3;
}


public class ChargingStationConnectorValidator : AbstractValidator<DtoChargingStationConnector>
{
    public ChargingStationConnectorValidator()
    {
        RuleFor(x => x.MaxCurrent)
            .NotEmpty();
        RuleFor(x => x.MinCurrent)
            .NotEmpty();
        RuleFor(x => x.ConnectedPhasesCount)
            .NotEmpty();
        RuleFor(x => x.Name)
            .NotEmpty();
        RuleFor(x => x)
            .Must(config => config.MaxCurrent >= config.MinCurrent)
            .WithMessage("Max Current must be greater than or equal to Min Current.");
    }
}
