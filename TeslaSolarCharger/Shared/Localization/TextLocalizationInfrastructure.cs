using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TeslaSolarCharger.Shared.Localization.Contracts;

namespace TeslaSolarCharger.Shared.Localization;

public record TextLocalizationTranslation(string Language, string Value);

public interface ITextLocalizationRegistry
{
    Type TargetType { get; }

    string? Get(string key, CultureInfo culture);
}

public abstract class TextLocalizationRegistry<T> : ITextLocalizationRegistry
{
    private readonly Dictionary<string, Dictionary<string, string>> _localizations =
        new(StringComparer.Ordinal);

    protected TextLocalizationRegistry()
    {
        Configure();
    }

    public Type TargetType => typeof(T);

    public string? Get(string key, CultureInfo culture)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (!_localizations.TryGetValue(key, out var translations))
        {
            return null;
        }

        foreach (var languageKey in GetLanguageFallbacks(culture))
        {
            if (translations.TryGetValue(languageKey, out var translation))
            {
                return translation;
            }
        }

        return null;
    }

    protected abstract void Configure();

    protected void Register(string key, params TextLocalizationTranslation[] translations)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key must be provided", nameof(key));
        }

        var registryTranslations = _localizations.GetValueOrDefault(key);
        if (registryTranslations == null)
        {
            registryTranslations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _localizations[key] = registryTranslations;
        }

        foreach (var translation in translations)
        {
            registryTranslations[translation.Language] = translation.Value;
        }
    }

    private static IEnumerable<string> GetLanguageFallbacks(CultureInfo culture)
    {
        var evaluatedLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(culture.Name) && evaluatedLanguages.Add(culture.Name))
        {
            yield return culture.Name;
        }

        var twoLetterLanguage = culture.TwoLetterISOLanguageName;
        if (!string.IsNullOrWhiteSpace(twoLetterLanguage) && evaluatedLanguages.Add(twoLetterLanguage))
        {
            yield return twoLetterLanguage;
        }

        if (evaluatedLanguages.Add(LanguageCodes.English))
        {
            yield return LanguageCodes.English;
        }
    }
}

public class TextLocalizationService : ITextLocalizationService
{
    private readonly Dictionary<Type, ITextLocalizationRegistry> _registries;

    public TextLocalizationService(IEnumerable<ITextLocalizationRegistry> registries)
    {
        _registries = registries.ToDictionary(registry => registry.TargetType);
    }

    public string? Get(string key, params Type[] registryTypes)
    {
        return Get(key, null, registryTypes);
    }

    public string? Get(string key, CultureInfo? culture, params Type[] registryTypes)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key must be provided", nameof(key));
        }

        if (registryTypes == null || registryTypes.Length == 0)
        {
            throw new ArgumentException("At least one registry type must be provided", nameof(registryTypes));
        }

        culture ??= CultureInfo.CurrentUICulture;
        foreach (var registryType in registryTypes)
        {
            if (registryType == null)
            {
                continue;
            }

            if (!_registries.TryGetValue(registryType, out var registry))
            {
                continue;
            }

            var value = registry.Get(key, culture);
            if (value != null)
            {
                return value;
            }
        }

        return null;
    }

    public string? Get<TRegistry>(string key, params Type[] fallbackRegistryTypes)
    {
        return Get<TRegistry>(key, null, fallbackRegistryTypes);
    }

    public string? Get<TRegistry>(string key, CultureInfo? culture, params Type[] fallbackRegistryTypes)
    {
        var registryTypes = new Type[1 + (fallbackRegistryTypes?.Length ?? 0)];
        registryTypes[0] = typeof(TRegistry);
        if (fallbackRegistryTypes != null && fallbackRegistryTypes.Length > 0)
        {
            Array.Copy(fallbackRegistryTypes, 0, registryTypes, 1, fallbackRegistryTypes.Length);
        }

        return Get(key, culture, registryTypes);
    }
}
