using System.Linq.Expressions;

namespace TeslaSolarCharger.Shared.Localization;

public static class LocalizedTextFactory
{
    public static LocalizedText Create(string english, string german)
    {
        var localizedText = new LocalizedText(english, german, null);
        LocalizedTextRegistry.Register(localizedText);
        return localizedText;
    }

    public static LocalizedText CreateForProperty<T>(Expression<Func<T, object?>> property, string english, string german)
    {
        var propertyName = PropertyExpressionHelper.GetPropertyName(property);
        var localizedText = new LocalizedText(english, german, propertyName);
        LocalizedTextRegistry.Register(localizedText);
        PropertyLocalizationRegistry.Register(propertyName, localizedText);
        return localizedText;
    }
}
