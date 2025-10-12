namespace TeslaSolarCharger.Shared.Localization;

public readonly record struct LocalizationKey(string Value)
{
    public override string ToString() => Value;
}
