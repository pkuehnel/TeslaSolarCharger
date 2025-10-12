using System.Reflection;

namespace TeslaSolarCharger.Shared.Localization;

public static class LocalizationKeyBuilder
{
    public static string DisplayName(PropertyInfo propertyInfo) => Build(propertyInfo, "DisplayName");

    public static string HelperText(PropertyInfo propertyInfo) => Build(propertyInfo, "HelperText");

    public static string FriendlyName(Type declaringType, string memberName) => $"{declaringType.FullName}.{memberName}.FriendlyName";

    public static string FriendlyName(string memberName) => $"FriendlyName.{memberName}";

    public static string EnumValue(Type enumType, string enumMemberName) => $"{enumType.FullName}.{enumMemberName}";

    public static string General(string category, string key) => $"{category}.{key}";

    private static string Build(PropertyInfo propertyInfo, string suffix)
    {
        if (propertyInfo.DeclaringType == null)
        {
            return $"{propertyInfo.Name}.{suffix}";
        }

        return $"{propertyInfo.DeclaringType.FullName}.{propertyInfo.Name}.{suffix}";
    }
}
