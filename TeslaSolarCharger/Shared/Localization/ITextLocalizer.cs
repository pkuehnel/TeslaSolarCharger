namespace TeslaSolarCharger.Shared.Localization;

public interface ITextLocalizer
{
    Language Language { get; }

    string Translate(LocalizedText text);

    string Translate(string englishKey);

    string Format(LocalizedText text, params object[] formatArguments);
}
