using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class PvValueLog
{
    public int Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public PvValueType Type { get; set; }
    public int IntValue { get; set; }
}
