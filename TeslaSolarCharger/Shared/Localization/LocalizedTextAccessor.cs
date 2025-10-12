using System.Reflection;

namespace TeslaSolarCharger.Shared.Localization;

public static class LocalizedTextAccessor
{
    public static LocalizedText Get(Type containerType, string memberName)
    {
        if (containerType == null)
        {
            throw new ArgumentNullException(nameof(containerType));
        }

        if (string.IsNullOrWhiteSpace(memberName))
        {
            throw new ArgumentException("Member name must be provided.", nameof(memberName));
        }

        var member = (MemberInfo?)containerType.GetProperty(memberName, BindingFlags.Public | BindingFlags.Static)
            ?? containerType.GetField(memberName, BindingFlags.Public | BindingFlags.Static);

        if (member == null)
        {
            throw new InvalidOperationException($"No public static member named '{memberName}' found on '{containerType.FullName}'.");
        }

        var value = member switch
        {
            PropertyInfo propertyInfo => propertyInfo.GetValue(null),
            FieldInfo fieldInfo => fieldInfo.GetValue(null),
            _ => null,
        };

        if (value is LocalizedText localizedText)
        {
            return localizedText;
        }

        throw new InvalidOperationException($"Member '{memberName}' on '{containerType.FullName}' does not provide a LocalizedText.");
    }
}
