using System.ComponentModel.DataAnnotations;

namespace TeslaSolarCharger.GridPriceProvider.Data.Options;

public class EnerginetOptions
{
    [Required]
    public string BaseUrl { get; set; }

    [Required]
    public EnerginetRegion Region { get; set; }

    [Required]
    public EnerginetCurrency Currency { get; set; }

    public decimal? VAT { get; set; }

    public FixedPriceOptions? FixedPrices { get; set; }
}

public enum EnerginetRegion
{
    DK1,
    DK2,
    NO2,
    SE3,
    SE4
}

public enum EnerginetCurrency
{
    DKK,
    EUR
}
