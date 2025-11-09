using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos.TemplateConfiguration;

public abstract class DtoTemplateValueConfiguration<TConfig> where TConfig : class
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ConfigurationVersion { get; set; }
    public int? MinRefreshIntervalMilliseconds { get; set; }
    public abstract TemplateValueGatherType GatherType { get; }
    public TConfig? Configuration { get; set; }
}
