namespace TeslaSolarCharger.Shared.Attributes;

/// <summary>
/// Marks a numeric property whose value may be negative. The numeric keypad iPhones show for numeric inputs has no
/// minus key, so inputs bound to such a property fall back to the text keyboard on iOS.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class AllowNegativeValuesAttribute : Attribute
{
}
