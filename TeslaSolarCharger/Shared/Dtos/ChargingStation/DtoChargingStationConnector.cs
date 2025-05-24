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
    [Postfix("A")]
    [HelperText("The minimum current that the charging point is allowed to use. Charging will never be slower than this current. Note: This value does not have any influence on when charging stops completly, you will find more details on \"Switch Off Current\". Recommended Value: 6.")]
    public int? MinCurrent { get; set; }
    [Postfix("A")]
    [HelperText("The charging point will stop charging when the available current drops below this value. This allows charging to continue for a while even if the current dips slightly, preventing unnecessary interruptions. Note: If you set this value to e.g. 3A while Min Current is set to 6A, charging will continue with 6A as long as there is enough solar power for 3A. Recommended Value: 6.")]
    public int? SwitchOffAtCurrent { get; set; }
    [Postfix("A")]
    [HelperText("The charging point will only begin charging when the available current exceeds this value. Helps to avoid starting the charging process too frequently due to small current fluctuations. Recommended Value: 8.")]
    public int? SwitchOnAtCurrent { get; set; }
    [Postfix("A")]
    [HelperText("The maximum current that the charging point is allowed to use. Charging will be limited to this value even if more current is available. Recommended Value: The maximum current permitted by the circuit breaker, wiring, and wallbox. If you're unsure about this value, please contact a qualified electrician.")]
    public int? MaxCurrent { get; set; }

    [HelperText("Number of connected phases on your charging station. If you're unsure about this value, please contact a qualified electrician. Note: Do not enter the number of phases the car can handle, just the number of phases the charging connector is connected to!")]
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
        RuleFor(x => x.SwitchOnAtCurrent)
            .Must((model, current) => current >= model.SwitchOffAtCurrent)
            .WithMessage("Switch On at Current must be at least as high as Switch off at Current.");

        RuleFor(x => x.MaxCurrent)
            .NotEmpty()
            .Must((model, max) => max >= model.MinCurrent)
            .WithMessage("Max Current must be greater than or equal to Min Current.");

        RuleFor(x => x.Name)
            .NotEmpty();
    }
}
