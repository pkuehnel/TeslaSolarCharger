using FluentValidation;
using TeslaSolarCharger.Shared.Attributes;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Generic;

namespace TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Kostal;

public class DtoKostalModbusConfiguration : DtoModbusConfigurationBase
{
    public DtoKostalModbusConfiguration()
    {
        Port = 1502;
        UnitId = 71;
    }

    /// <summary>
    /// When enabled TSC can block discharging and force charging of the home battery. Requires the external battery
    /// management via Modbus to be activated in the inverter's installer settings.
    /// </summary>
    public bool EnableHomeBatteryControl { get; set; }
    [Postfix("W")]
    public int MaxBatteryChargePowerW { get; set; } = 4200;
}

public class DtoKostalModbusConfigurationValidator : DtoModbusConfigurationBaseValidator<DtoKostalModbusConfiguration>
{
    public DtoKostalModbusConfigurationValidator()
    {
        RuleFor(x => x.MaxBatteryChargePowerW).GreaterThan(0).When(x => x.EnableHomeBatteryControl);
    }
}
