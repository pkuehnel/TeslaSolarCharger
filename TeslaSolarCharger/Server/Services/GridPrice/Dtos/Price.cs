using TeslaSolarCharger.Server.Dtos;

namespace TeslaSolarCharger.Server.Services.GridPrice.Dtos;

public class Price : ValidFromToBase
{
    public decimal GridPrice { get; set; }
    public decimal SolarPrice { get; set; }
}

