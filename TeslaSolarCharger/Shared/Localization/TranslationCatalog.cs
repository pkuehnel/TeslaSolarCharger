using System;
using System.Collections.Generic;
using System.Globalization;

namespace TeslaSolarCharger.Shared.Localization;

public static partial class TranslationCatalog
{
    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> Translations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["de"] = CreateGermanDictionary(),
    };

    public static bool TryGetTranslation(CultureInfo culture, string englishText, out string translation)
    {
        foreach (var cultureName in EnumerateCultureNames(culture))
        {
            if (Translations.TryGetValue(cultureName, out var dictionary) && dictionary.TryGetValue(englishText, out translation))
            {
                return true;
            }
        }

        translation = englishText;
        return false;
    }

    private static IEnumerable<string> EnumerateCultureNames(CultureInfo culture)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = culture;
        while (current != CultureInfo.InvariantCulture)
        {
            if (!string.IsNullOrEmpty(current.Name) && seen.Add(current.Name))
            {
                yield return current.Name;
            }

            current = current.Parent;
        }

        if (!string.IsNullOrEmpty(culture.TwoLetterISOLanguageName) && seen.Add(culture.TwoLetterISOLanguageName))
        {
            yield return culture.TwoLetterISOLanguageName;
        }
    }

    private static partial Dictionary<string, string> CreateGermanDictionary();
}
