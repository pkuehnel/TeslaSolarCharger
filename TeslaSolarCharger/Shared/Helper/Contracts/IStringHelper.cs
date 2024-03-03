namespace TeslaSolarCharger.Shared.Helper.Contracts;

public interface IStringHelper
{
    string MakeNonWhiteSpaceCapitalString(string inputString);
    string GenerateFriendlyStringWithOutIdSuffix(string inputString);
    string GenerateFriendlyStringFromPascalString(string inputString);
}
