using Microsoft.Extensions.Localization;
using TeslaSolarCharger.Shared.Localization.Contracts;

namespace TeslaSolarCharger.Shared.Localization;

public class AppLocalizationService : IAppLocalizationService
{
    private readonly IStringLocalizer<AppResource> _localizer;

    public AppLocalizationService(IStringLocalizer<AppResource> localizer)
    {
        _localizer = localizer;
    }

    public string this[string key] => GetString(key);

    public string GetString(string key, string? defaultValue = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return defaultValue ?? string.Empty;
        }

        var localized = _localizer[key];
        if (localized.ResourceNotFound)
        {
            return defaultValue ?? key;
        }

        return localized.Value;
    }

    public string GetString(string key, string? defaultValue, params object[] arguments)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return defaultValue ?? string.Empty;
        }

        var localized = _localizer[key, arguments];
        if (localized.ResourceNotFound)
        {
            return defaultValue == null ? string.Format(key, arguments) : string.Format(defaultValue, arguments);
        }

        return localized.Value;
    }
}
