using FluentValidation;
using TeslaSolarCharger.Shared.Attributes;

namespace TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.TeslaPowerwall;

public class DtoTeslaPowerwallTemplateValueConfiguration
{
    public long? EnergySiteId { get; set; }

    /// <summary>
    /// When enabled TSC can block discharging and force charging of the Powerwall by adjusting the backup reserve.
    /// </summary>
    public bool EnableHomeBatteryControl { get; set; }
    /// <summary>
    /// Backup reserve percent that is restored when no battery mode is forced.
    /// </summary>
    [Postfix("%")]
    public int NormalModeBackupReservePercent { get; set; } = 20;
}


public class DtoTeslaPowerwallTemplateValueConfigurationValidator : AbstractValidator<DtoTeslaPowerwallTemplateValueConfiguration>
{
    public DtoTeslaPowerwallTemplateValueConfigurationValidator()
    {
        RuleFor(x => x.EnergySiteId).NotEmpty();
        RuleFor(x => x.NormalModeBackupReservePercent).InclusiveBetween(0, 100);
    }
}
