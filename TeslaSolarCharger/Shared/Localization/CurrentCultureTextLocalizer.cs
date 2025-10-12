using System.Globalization;
using System.Linq;

namespace TeslaSolarCharger.Shared.Localization;

public class CurrentCultureTextLocalizer : ITextLocalizer
{
    public Language Language => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
    {
        "de" => Language.German,
        _ => Language.English,
    };

    public string Translate(LocalizedText text) => text.Translate(CultureInfo.CurrentUICulture);

    public string Translate(string englishKey) => LocalizedTextRegistry.Translate(englishKey, CultureInfo.CurrentUICulture);

    public string Format(LocalizedText text, params object[] formatArguments)
    {
        var translatedArguments = formatArguments.Select(argument => argument switch
        {
            LocalizedText localizedText => Translate(localizedText),
            IFormattable formattable => formattable.ToString(null, CultureInfo.CurrentUICulture),
            _ => argument?.ToString() ?? string.Empty,
        }).ToArray();

        var template = Translate(text);
        return string.Format(CultureInfo.CurrentUICulture, template, translatedArguments);
    }
}
