using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using TeslaSolarCharger.Shared.Helper.Contracts;

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
        var friendlyString = GenerateFriendlyStringFromPascalString(inputString);
        if (friendlyString.EndsWith(" Id"))
        {
            return friendlyString[..^3];
        }
        else if (friendlyString.EndsWith(" Ids"))
        {
            return friendlyString[..^4] + "s";
        }
        else
        {
            return friendlyString;
        }
    }

    private string GenerateFriendlyStringFromPascalString(string inputString)
    {
        return Regex.Replace(inputString, "(\\B[A-Z])", " $1");
    }
}
