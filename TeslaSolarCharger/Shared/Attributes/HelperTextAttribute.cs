using System.Globalization;
using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Attributes;

public class HelperTextAttribute : Attribute
{
    private readonly string _fallbackHelperText;
    private readonly LocalizedText? _localizedText;

    public HelperTextAttribute()
    {
        _fallbackHelperText = string.Empty;
    }

    public HelperTextAttribute(string helperText)
    {
        _fallbackHelperText = helperText ?? string.Empty;
    }

    public HelperTextAttribute(Type localizedTextContainerType, string memberName)
    {
        _localizedText = LocalizedTextAccessor.Get(localizedTextContainerType, memberName);
        _fallbackHelperText = _localizedText.Value.English;
    }

    public string HelperText => _localizedText?.Translate(CultureInfo.CurrentUICulture) ?? _fallbackHelperText;

    public string GetHelperText(Language language) => _localizedText?.Translate(language) ?? _fallbackHelperText;
}
