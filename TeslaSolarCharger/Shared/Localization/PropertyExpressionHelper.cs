using System.Linq.Expressions;
using System.Reflection;

namespace TeslaSolarCharger.Shared.Localization;

internal static class PropertyExpressionHelper
{
    public static string GetPropertyName<T>(Expression<Func<T, object?>> expression)
    {
        if (expression.Body is MemberExpression memberExpression)
        {
            return ValidateMember(memberExpression.Member);
        }

        if (expression.Body is UnaryExpression { Operand: MemberExpression innerMember })
        {
            return ValidateMember(innerMember.Member);
        }

        throw new ArgumentException("Expression must reference a property.", nameof(expression));
    }

    private static string ValidateMember(MemberInfo memberInfo)
    {
        if (memberInfo is PropertyInfo propertyInfo)
        {
            return propertyInfo.Name;
        }

        throw new ArgumentException("Expression must reference a property.");
    }
}
