using System;
using System.Globalization;
using TeslaSolarCharger.Shared.Localization.Contracts;
using TeslaSolarCharger.Shared.Localization.Registries;
using TeslaSolarCharger.Shared.Localization.Registries.Server;

namespace TeslaSolarCharger.Shared.Dtos.Home;

public class NotChargingWithExpectedPowerReasonTemplate
{
    public NotChargingWithExpectedPowerReasonTemplate()
    {
    }

    public NotChargingWithExpectedPowerReasonTemplate(string localizationKey, params object?[] formatArguments)
    {
        LocalizationKey = localizationKey ?? throw new ArgumentNullException(nameof(localizationKey));
        if (formatArguments?.Length > 0)
        {
            FormatArguments = formatArguments;
        }
    }

    public string LocalizationKey { get; set; } = string.Empty;

    public object?[]? FormatArguments { get; set; }

    public DateTimeOffset? ReasonEndTime { get; set; }

    public NotChargingWithExpectedPowerReasonTemplate Clone()
    {
        return new NotChargingWithExpectedPowerReasonTemplate(LocalizationKey)
        {
            FormatArguments = FormatArguments?.ToArray(),
            ReasonEndTime = ReasonEndTime,
        };
    }

    public DtoNotChargingWithExpectedPowerReason ToDto(ITextLocalizationService textLocalizationService, CultureInfo culture)
    {
        if (textLocalizationService == null)
        {
            throw new ArgumentNullException(nameof(textLocalizationService));
        }

        var template = textLocalizationService.Get<NotChargingWithExpectedPowerReasonLocalizationRegistry>(LocalizationKey, culture, typeof(SharedComponentLocalizationRegistry))
                       ?? LocalizationKey;

        var formattedReason = (FormatArguments?.Length ?? 0) > 0
            ? string.Format(culture, template, FormatArguments!)
            : template;

        return new DtoNotChargingWithExpectedPowerReason(formattedReason, ReasonEndTime);
    }
}
