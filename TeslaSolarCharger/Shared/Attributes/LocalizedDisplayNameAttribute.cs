using System.ComponentModel;
using System.Globalization;
using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Attributes;

public sealed class LocalizedDisplayNameAttribute : DisplayNameAttribute
{
    private readonly LocalizedText _localizedText;

    public LocalizedDisplayNameAttribute(Type localizedTextContainerType, string memberName)
        : base(LocalizedTextAccessor.Get(localizedTextContainerType, memberName).English)
    {
        _localizedText = LocalizedTextAccessor.Get(localizedTextContainerType, memberName);
    }

    public override string DisplayName => _localizedText.Translate(CultureInfo.CurrentUICulture);

    public string GetDisplayName(Language language) => _localizedText.Translate(language);
}
