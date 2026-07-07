using FluentValidation;

namespace TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Generic;

/// <summary>
/// Shared configuration for all vendors whose values are gathered via fixed Modbus TCP register maps
/// (see GenericModbusTemplateSettings for the per vendor defaults and ModbusTemplateDefinitions for the register maps).
/// </summary>
public class DtoGenericModbusTemplateValueConfiguration : DtoModbusConfigurationBase
{
    /// <summary>
    /// Only relevant for vendors supporting battery control. When enabled TSC can block discharging and force
    /// charging of the home battery.
    /// </summary>
    public bool EnableHomeBatteryControl { get; set; }
    public int MaxBatteryChargePowerW { get; set; } = 4200;
    public int MaxBatteryDischargePowerW { get; set; } = 4200;
}

public class DtoGenericModbusTemplateValueConfigurationValidator : DtoModbusConfigurationBaseValidator<DtoGenericModbusTemplateValueConfiguration>
{
    public DtoGenericModbusTemplateValueConfigurationValidator()
    {
        RuleFor(x => x.MaxBatteryChargePowerW).GreaterThan(0).When(x => x.EnableHomeBatteryControl);
        RuleFor(x => x.MaxBatteryDischargePowerW).GreaterThan(0).When(x => x.EnableHomeBatteryControl);
    }
}
