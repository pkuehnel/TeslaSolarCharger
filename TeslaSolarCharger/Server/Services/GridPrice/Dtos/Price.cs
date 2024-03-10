namespace TeslaSolarCharger.Server.Services.GridPrice.Dtos;

public class Price
{
    public decimal Value { get; set; }
    public decimal SolarPrice { get; set; }

    public DateTimeOffset ValidFrom { get; set; }

    public DateTimeOffset ValidTo { get; set; }
}
