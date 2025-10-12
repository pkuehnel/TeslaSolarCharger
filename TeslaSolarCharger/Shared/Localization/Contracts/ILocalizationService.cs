using System.Globalization;

namespace TeslaSolarCharger.Shared.Localization.Contracts;

public interface ILocalizationService
{
    string Translate(string englishText);

    string Translate(string englishText, params object[] formatArguments);

    string Translate(LocalizedStringKey key);

    string Translate(LocalizedStringKey key, params object[] formatArguments);

    CultureInfo GetCurrentCulture();
}
