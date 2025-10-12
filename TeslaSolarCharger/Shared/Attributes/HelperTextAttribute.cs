namespace TeslaSolarCharger.Shared.Attributes;

public class HelperTextAttribute : Attribute
{
    public HelperTextAttribute()
    {
    }

    public HelperTextAttribute(string helperText)
    {
        HelperText = helperText;
    }

    /// <summary>
    /// Gets the default helper text that should be used when no localization entry exists.
    /// </summary>
    public string? HelperText { get; }

    /// <summary>
    /// Optional custom localization key. When left <c>null</c> a key is generated from the property metadata.
    /// </summary>
    public string? LocalizationKey { get; init; }
}
