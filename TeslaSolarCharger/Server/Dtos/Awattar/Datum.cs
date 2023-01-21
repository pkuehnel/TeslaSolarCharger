// ReSharper disable All
namespace TeslaSolarCharger.Server.Dtos.Awattar;

#pragma warning disable CS8618
public class Datum
{
    public long start_timestamp { get; set; }
    public long end_timestamp { get; set; }
    public decimal marketprice { get; set; }
    public string unit { get; set; }
}
