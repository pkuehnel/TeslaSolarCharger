namespace TeslaSolarCharger.Shared.Helper.Contracts;

using System;

public interface IStringHelper
{
    string MakeNonWhiteSpaceCapitalString(string inputString);
    string GenerateFriendlyStringWithOutIdSuffix(string inputString, Type? contextType = null);
    string GenerateFriendlyStringFromPascalString(string inputString, Type? contextType = null);
}
