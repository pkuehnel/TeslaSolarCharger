namespace TeslaSolarCharger.GridPriceProvider.Data;

public class Price
{
    public decimal Value { get; set; }

    public DateTimeOffset ValidFrom { get; set; }

    public DateTimeOffset ValidTo { get; set; }
}
