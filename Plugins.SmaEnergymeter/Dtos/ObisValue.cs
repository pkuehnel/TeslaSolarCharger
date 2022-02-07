using Plugins.SmaEnergymeter.Enums;

namespace Plugins.SmaEnergymeter.Dtos;

public class ObisValue
{
    public int Id { get; set; }
    public ValueMode ValueType { get; set; }
    public ulong Value { get; set; }
}