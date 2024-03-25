namespace TeslaSolarCharger.Shared.Attributes;

public class HelperTextAttribute : Attribute
{
    public string HelperText { get; set; }

    public HelperTextAttribute()
    {
        HelperText = string.Empty;
    }

    public HelperTextAttribute(string helperText)
    {
        HelperText = helperText;
    }
}
