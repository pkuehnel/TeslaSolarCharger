// ReSharper disable InconsistentNaming
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
namespace TeslaSolarCharger.Server.Dtos.EnergyCharts;

public class DtoEnergyChartPrices
{
    public List<int> unix_seconds { get; set; }
    public List<decimal> price { get; set; }
    public string unit { get; set; }
    public bool deprecated { get; set; }
}
