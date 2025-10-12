using System;

namespace TeslaSolarCharger.Shared.Localization;

public readonly record struct LocalizedStringKey
{
    public LocalizedStringKey(string englishText)
    {
        EnglishText = englishText ?? throw new ArgumentNullException(nameof(englishText));
    }

    public string EnglishText { get; }

    public override string ToString() => EnglishText;

    public static implicit operator string(LocalizedStringKey key) => key.EnglishText;

    public static implicit operator LocalizedStringKey(string englishText) => new(englishText);
}
