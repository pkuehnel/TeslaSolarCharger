using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using TeslaSolarCharger.Shared.Helper.Contracts;
using TeslaSolarCharger.Shared.Localization.Contracts;

namespace TeslaSolarCharger.Shared.Helper;

public class StringHelper(ILogger<StringHelper> logger, ILocalizationService localizationService) : IStringHelper
{
    public string MakeNonWhiteSpaceCapitalString(string inputString)
    {
        logger.LogTrace("{method}({inputString})", nameof(MakeNonWhiteSpaceCapitalString), inputString);
        return string.Concat(inputString.ToUpper().Where(c => !char.IsWhiteSpace(c)));
    }

    public string GenerateFriendlyStringWithOutIdSuffix(string inputString)
    {
        logger.LogTrace("{method}({inputString})", nameof(GenerateFriendlyStringWithOutIdSuffix), inputString);
        var friendlyString = CreateFriendlyStringFromPascalString(inputString);
        if (friendlyString.EndsWith(" Id"))
        {
            friendlyString = friendlyString[..^3];
        }
        else if (friendlyString.EndsWith(" Ids"))
        {
            friendlyString = friendlyString[..^4] + "s";
        }

        return localizationService.Translate(friendlyString);
    }

    public string GenerateFriendlyStringFromPascalString(string inputString)
    {
        var friendlyString = CreateFriendlyStringFromPascalString(inputString);
        return localizationService.Translate(friendlyString);
    }

    private static string CreateFriendlyStringFromPascalString(string inputString) => Regex.Replace(inputString, "(\\B[A-Z])", " $1");
}
