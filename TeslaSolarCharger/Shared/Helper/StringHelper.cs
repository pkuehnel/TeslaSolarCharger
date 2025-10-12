using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using TeslaSolarCharger.Shared.Helper.Contracts;
using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Helper;

public class StringHelper : IStringHelper
{
    private readonly ILogger<StringHelper> _logger;
    private readonly IAppStringLocalizer _localizer;

    public StringHelper(ILogger<StringHelper> logger, IAppStringLocalizer localizer)
    {
        _logger = logger;
        _localizer = localizer;
    }

    public string MakeNonWhiteSpaceCapitalString(string inputString)
    {
        _logger.LogTrace("{method}({inputString})", nameof(MakeNonWhiteSpaceCapitalString), inputString);
        return string.Concat(inputString.ToUpper().Where(c => !char.IsWhiteSpace(c)));
    }

    public string GenerateFriendlyStringWithOutIdSuffix(string inputString)
    {
        _logger.LogTrace("{method}({inputString})", nameof(GenerateFriendlyStringWithOutIdSuffix), inputString);
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

    public string GenerateFriendlyStringFromPascalString(string inputString)
    {
        if (_localizer.TryGetValue(inputString, out var localizedValue) && !string.IsNullOrWhiteSpace(localizedValue))
        {
            return localizedValue;
        }

        return Regex.Replace(inputString, "(\\B[A-Z])", " $1");
    }
}
