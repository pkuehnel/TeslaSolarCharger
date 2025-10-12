using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TeslaSolarCharger.Shared.Attributes;
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

    public string GenerateFriendlyStringFromPascalString(string inputString)
    {
        return Regex.Replace(inputString, "(\\B[A-Z])", " $1");
    }

    public string GetDisplayNameFor(LambdaExpression propertyExpression)
    {
        ArgumentNullException.ThrowIfNull(propertyExpression);
        logger.LogTrace("{method}({expression})", nameof(GetDisplayNameFor), propertyExpression);
        var propertyInfo = GetPropertyInfo(propertyExpression);
        var displayNameAttribute = propertyInfo.GetCustomAttribute<DisplayNameAttribute>();
        if (displayNameAttribute != null && !string.IsNullOrWhiteSpace(displayNameAttribute.DisplayName))
        {
            return displayNameAttribute.DisplayName;
        }

        return GenerateFriendlyStringWithOutIdSuffix(propertyInfo.Name);
    }

    public string? GetHelperTextFor(LambdaExpression propertyExpression)
    {
        ArgumentNullException.ThrowIfNull(propertyExpression);
        logger.LogTrace("{method}({expression})", nameof(GetHelperTextFor), propertyExpression);
        var propertyInfo = GetPropertyInfo(propertyExpression);
        return propertyInfo.GetCustomAttribute<HelperTextAttribute>()?.HelperText;
    }

    private static PropertyInfo GetPropertyInfo(LambdaExpression propertyExpression)
    {
        Expression body = propertyExpression.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked, Operand: MemberExpression unaryMember })
        {
            body = unaryMember;
        }

        if (body is not MemberExpression memberExpression)
        {
            throw new ArgumentException("Expression must target a property", nameof(propertyExpression));
        }

        if (memberExpression.Member is not PropertyInfo propertyInfo)
        {
            throw new ArgumentException("Expression must target a property", nameof(propertyExpression));
        }

        return propertyInfo;
    }
}
