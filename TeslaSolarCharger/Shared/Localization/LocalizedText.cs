using System.Globalization;

namespace TeslaSolarCharger.Shared.Localization;

public readonly record struct LocalizedText
{
    public LocalizedText()
    {
        throw new InvalidOperationException("Use LocalizedTextFactory to create instances of LocalizedText.");
    }

    internal LocalizedText(string english, string german, string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(english))
        {
            throw new ArgumentException("English text must be provided.", nameof(english));
        }

        if (string.IsNullOrWhiteSpace(german))
        {
            throw new ArgumentException("German text must be provided.", nameof(german));
        }

        Key = english;
        German = german;
        PropertyName = propertyName;
    }

    public string Key { get; }

    public string English => Key;

    public string German { get; }

    public string? PropertyName { get; }

    public string Translate(CultureInfo culture) => culture.TwoLetterISOLanguageName switch
    {
        "de" => German,
        _ => English,
    };

    public string Translate(Language language) => language switch
    {
        Language.German => German,
        _ => English,
    };

    public override string ToString() => English;
}
