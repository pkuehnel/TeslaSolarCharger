using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.Logging;
using TeslaSolarCharger.Shared.Localization.Contracts;

namespace TeslaSolarCharger.Shared.Localization;

public class LocalizationService(ILogger<LocalizationService> logger) : ILocalizationService
{
    public CultureInfo GetCurrentCulture() => CultureInfo.CurrentUICulture;

    public string Translate(string englishText) => TranslateInternal(englishText, Array.Empty<object>());

    public string Translate(string englishText, params object[] formatArguments) => TranslateInternal(englishText, formatArguments);

    public string Translate(LocalizedStringKey key) => Translate(key.EnglishText);

    public string Translate(LocalizedStringKey key, params object[] formatArguments) => Translate(key.EnglishText, formatArguments);

    private string TranslateInternal(string englishText, IReadOnlyList<object> formatArguments)
    {
        if (string.IsNullOrWhiteSpace(englishText))
        {
            return englishText;
        }

        var currentCulture = CultureInfo.CurrentUICulture;
        if (!TranslationCatalog.TryGetTranslation(currentCulture, englishText, out var translation))
        {
            logger.LogDebug("Missing translation for '{EnglishText}' in culture {Culture}", englishText, currentCulture);
            translation = englishText;
        }

        if (formatArguments.Count > 0)
        {
            try
            {
                return string.Format(currentCulture, translation, formatArguments.ToArray());
            }
            catch (FormatException ex)
            {
                logger.LogWarning(ex, "Failed to format translation for '{EnglishText}'", englishText);
                return string.Format(CultureInfo.InvariantCulture, englishText, formatArguments.ToArray());
            }
        }

        return translation;
    }
}
