using System;
using System.Globalization;

namespace TeslaSolarCharger.Shared.Localization.Contracts;

public interface ITextLocalizationService
{
    string? Get(string key, params Type[] registryTypes);

    string? Get(string key, CultureInfo? culture, params Type[] registryTypes);

    string? Get<TRegistry>(string key, params Type[] fallbackRegistryTypes);

    string? Get<TRegistry>(string key, CultureInfo? culture, params Type[] fallbackRegistryTypes);
}
