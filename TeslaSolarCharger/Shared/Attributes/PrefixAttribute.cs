namespace TeslaSolarCharger.Shared.Attributes;

public class PrefixAttribute: Attribute
{
    public string Prefix { get; set; }

    public PrefixAttribute()
    {
        Prefix = string.Empty;
    }
    
    public PrefixAttribute(string prefix)
    {
        Prefix = prefix;
    }
}
