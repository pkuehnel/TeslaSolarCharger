namespace TeslaSolarCharger.Server.Dtos;

public abstract class ValidFromToBase
{
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset ValidTo { get; set; }
}
