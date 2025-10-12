using System.Collections.Concurrent;
using System.Globalization;

namespace TeslaSolarCharger.Shared.Localization;

public static class PropertyLocalizationRegistry
{
    private static readonly ConcurrentDictionary<string, LocalizedText> PropertyTexts = new(StringComparer.Ordinal);

    public static void Register(string propertyName, LocalizedText text)
    {
        PropertyTexts.AddOrUpdate(propertyName, text, (_, current) =>
        {
            if (current != text)
            {
                throw new InvalidOperationException($"A different translation for property '{propertyName}' is already registered.");
            }

            return current;
        });
    }

    public static bool TryGet(string propertyName, out LocalizedText text) => PropertyTexts.TryGetValue(propertyName, out text);

    public static string Translate(string propertyName, CultureInfo culture) =>
        TryGet(propertyName, out var text)
            ? text.Translate(culture)
            : propertyName;
}
