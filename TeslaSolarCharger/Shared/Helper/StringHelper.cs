using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.RegularExpressions;
using TeslaSolarCharger.Shared.Helper.Contracts;
using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Helper;

public class StringHelper(ILogger<StringHelper> logger) : IStringHelper
{
    public string MakeNonWhiteSpaceCapitalString(string inputString)
    {
        logger.LogTrace("{method}({inputString})", nameof(MakeNonWhiteSpaceCapitalString), inputString);
        return string.Concat(inputString.ToUpper().Where(c => !char.IsWhiteSpace(c)));
    }

    public string GenerateFriendlyStringWithOutIdSuffix(string inputString)
    {
        logger.LogTrace("{method}({inputString})", nameof(GenerateFriendlyStringWithOutIdSuffix), inputString);

        if (PropertyLocalizationRegistry.TryGet(inputString, out var propertyText))
        {
            return propertyText.Translate(CultureInfo.CurrentUICulture);
        }

        var friendlyString = GenerateFriendlyStringFromPascalString(inputString);
        if (friendlyString.EndsWith(" Id", StringComparison.Ordinal))
        {
            return friendlyString[..^3];
        }
        else if (friendlyString.EndsWith(" Ids", StringComparison.Ordinal))
        {
            return friendlyString[..^4] + "s";
        }
        else
        {
            return friendlyString;
        }
    }

    public string GenerateFriendlyStringFromPascalString(string inputString)
    {
        if (PropertyLocalizationRegistry.TryGet(inputString, out var propertyText))
        {
            return propertyText.Translate(CultureInfo.CurrentUICulture);
        }

        var friendlyString = Regex.Replace(inputString, "(\\B[A-Z])", " $1");
        if (LocalizedTextRegistry.TryGet(friendlyString, out var localizedText))
        {
            return localizedText.Translate(CultureInfo.CurrentUICulture);
        }

        return friendlyString;
    }
}
