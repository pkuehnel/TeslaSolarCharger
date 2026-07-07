using FluentValidation;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Generic;

namespace TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Sma;

public class DtoSmaInverterTemplateValueConfiguration : DtoModbusConfigurationBase
{
    public DtoSmaInverterTemplateValueConfiguration()
    {
        Port = 502;
        UnitId = 3;
    }

    /// <summary>
    /// Only relevant for hybrid inverters. When enabled TSC can block discharging and force charging of the home battery.
    /// </summary>
    public bool EnableHomeBatteryControl { get; set; }
    public int MaxBatteryChargePowerW { get; set; } = 4200;
    public int MaxBatteryDischargePowerW { get; set; } = 4200;
}

public class DtoSmaInverterTemplateValueConfigurationValidator : DtoModbusConfigurationBaseValidator<DtoSmaInverterTemplateValueConfiguration>
{
    public DtoSmaInverterTemplateValueConfigurationValidator()
    {
        RuleFor(x => x.MaxBatteryChargePowerW).GreaterThan(0).When(x => x.EnableHomeBatteryControl);
        RuleFor(x => x.MaxBatteryDischargePowerW).GreaterThan(0).When(x => x.EnableHomeBatteryControl);
    }
}
