// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006
#pragma warning disable CS8618
namespace TeslaSolarCharger.Server.Dtos.Awattar;

public class DtoAwattarPrices
{
    public string @object { get; set; }
    // ReSharper disable once CollectionNeverUpdated.Global
    public List<Datum> data { get; set; }
    public string url { get; set; }
}
// ReSharper disable once ClassNeverInstantiated.Global
