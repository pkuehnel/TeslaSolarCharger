using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Server.Services.GridPrice.Dtos;

public class Price : ValidFromToBase
{
    public Price()
    {
        
    }

    //This constructor is required to the GetCopy method throws an exception as soon as any property is added
    public Price(DateTimeOffset validFrom, DateTimeOffset validTo, decimal gridPrice, decimal solarPrice, bool isSpotPriceBased)
    {
        ValidFrom = validFrom;
        ValidTo = validTo;
        GridPrice = gridPrice;
        SolarPrice = solarPrice;
        IsSpotPriceBased = isSpotPriceBased;
    }

    public decimal GridPrice { get; set; }
    public decimal SolarPrice { get; set; }
    public bool IsSpotPriceBased { get; set; }
    // Add all new properties as a parameter to the constructor above so you get a compile error in the GetCopy method
}

