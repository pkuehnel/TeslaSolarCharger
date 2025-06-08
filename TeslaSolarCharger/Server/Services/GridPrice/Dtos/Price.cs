using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Server.Services.GridPrice.Dtos;

public class Price : ValidFromToBase
{
    public decimal GridPrice { get; set; }
    public decimal SolarPrice { get; set; }
}

