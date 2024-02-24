namespace TeslaSolarCharger.Shared.Attributes;

public class PostfixAttribute : Attribute
{
    public string Postfix { get; set; }

    public PostfixAttribute()
    {
        Postfix = string.Empty;
    }
    
    public PostfixAttribute(string postfix)
    {
        Postfix = postfix;
    }
}
