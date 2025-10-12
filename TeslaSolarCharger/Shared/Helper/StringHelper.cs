using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using TeslaSolarCharger.Shared.Helper.Contracts;
using TeslaSolarCharger.Shared.Localization;
using TeslaSolarCharger.Shared.Localization.Contracts;

namespace TeslaSolarCharger.Shared.Helper;

public class StringHelper : IStringHelper
{
    private readonly ILogger<StringHelper> _logger;
    private readonly IAppLocalizationService _localizationService;

    public StringHelper(ILogger<StringHelper> logger, IAppLocalizationService localizationService)
    {
        _logger = logger;
        _localizationService = localizationService;
    }

    public string MakeNonWhiteSpaceCapitalString(string inputString)
    {
        _logger.LogTrace("{method}({inputString})", nameof(MakeNonWhiteSpaceCapitalString), inputString);
        return string.Concat(inputString.ToUpper().Where(c => !char.IsWhiteSpace(c)));
    }

    public string GenerateFriendlyStringWithOutIdSuffix(string inputString, Type? contextType = null)
    {
        _logger.LogTrace("{method}({inputString}, {context})", nameof(GenerateFriendlyStringWithOutIdSuffix), inputString, contextType);
        var friendlyString = GenerateFriendlyStringFromPascalString(inputString, contextType);
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

    public string GenerateFriendlyStringFromPascalString(string inputString, Type? contextType = null)
    {
        var defaultValue = Regex.Replace(inputString, "(\\B[A-Z])", " $1");

        if (contextType != null)
        {
            var key = contextType.IsEnum
                ? LocalizationKeyBuilder.EnumValue(contextType, inputString)
                : LocalizationKeyBuilder.FriendlyName(contextType, inputString);

            return _localizationService.GetString(key, defaultValue);
        }

        return _localizationService.GetString(LocalizationKeyBuilder.FriendlyName(inputString), defaultValue);
    }
}
