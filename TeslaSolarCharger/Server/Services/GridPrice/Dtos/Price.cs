using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Server.Services.GridPrice.Dtos;

public class Price : ValidFromToBase
{
    public Price()
    {
        
    }

    // Using this constructor in the `GetCopy` method ensures that adding new properties to this class will cause a compile error there, reminding the developer to update the copy logic.
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

