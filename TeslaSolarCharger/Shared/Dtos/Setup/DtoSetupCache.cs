using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

namespace TeslaSolarCharger.Shared.Dtos.Setup;

public class DtoSetupCache
{
    public int CurrentStep { get; set; }
    public bool HasPvSystem { get; set; }
    public DtoBaseConfiguration Configuration { get; set; } = new();
}
