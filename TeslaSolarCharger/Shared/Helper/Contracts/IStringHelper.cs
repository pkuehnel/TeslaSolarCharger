using System.Linq.Expressions;

namespace TeslaSolarCharger.Shared.Helper.Contracts;

public interface IStringHelper
{
    string MakeNonWhiteSpaceCapitalString(string inputString);
    string GenerateFriendlyStringWithOutIdSuffix(string inputString);
    string GenerateFriendlyStringFromPascalString(string inputString);
    string GetDisplayNameFor(LambdaExpression propertyExpression);
    string? GetHelperTextFor(LambdaExpression propertyExpression);
}
