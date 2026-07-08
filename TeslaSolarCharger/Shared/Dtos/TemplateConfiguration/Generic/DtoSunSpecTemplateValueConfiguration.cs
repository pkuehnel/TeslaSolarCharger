using FluentValidation;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Generic;

namespace TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Generic;

/// <summary>
/// Shared configuration for all vendors whose values are gathered via SunSpec Modbus TCP
/// (see SunSpecTemplateSettings for the per vendor defaults and SunSpecTemplateDefinitions for the value maps).
/// </summary>
public class DtoSunSpecTemplateValueConfiguration : DtoModbusConfigurationBase
{
    public bool EnableHomeBatteryControl { get; set; }
    /// <summary>
    /// Charge rate in percent for SunSpec model 124 based control (device charges at this percentage of its max rate).
    /// </summary>
    public int MaxChargeRatePercent { get; set; } = 100;
    /// <summary>
    /// Max charge power in watts for vendors using plain register control (e.g. Kostal Plenticore Gen2).
    /// </summary>
    public int MaxBatteryChargePowerW { get; set; } = 4200;
}

public class DtoSunSpecTemplateValueConfigurationValidator : DtoModbusConfigurationBaseValidator<DtoSunSpecTemplateValueConfiguration>
{
    public DtoSunSpecTemplateValueConfigurationValidator()
    {
        RuleFor(x => x.MaxChargeRatePercent).InclusiveBetween(1, 100).When(x => x.EnableHomeBatteryControl);
        RuleFor(x => x.MaxBatteryChargePowerW).GreaterThan(0).When(x => x.EnableHomeBatteryControl);
    }
}
