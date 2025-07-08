namespace TeslaSolarCharger.Shared.Dtos;

public abstract class ValidFromToBase
{
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset ValidTo { get; set; }
}
