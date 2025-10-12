using System.Collections.Concurrent;
using System.Globalization;

namespace TeslaSolarCharger.Shared.Localization;

public static class LocalizedTextRegistry
{
    private static readonly ConcurrentDictionary<string, LocalizedText> Texts = new(StringComparer.Ordinal);

    public static void Register(LocalizedText text)
    {
        var existing = Texts.AddOrUpdate(text.Key, text, (_, current) =>
        {
            if (current != text)
            {
                throw new InvalidOperationException($"Localized text for '{text.Key}' is already registered with different translations.");
            }

            return current;
        });

        if (existing != text && existing.Key != text.Key)
        {
            throw new InvalidOperationException($"Unable to register localized text for '{text.Key}'.");
        }
    }

    public static bool TryGet(string englishKey, out LocalizedText text) => Texts.TryGetValue(englishKey, out text);

    public static string Translate(string englishKey, CultureInfo culture) =>
        TryGet(englishKey, out var localizedText)
            ? localizedText.Translate(culture)
            : englishKey;
}
