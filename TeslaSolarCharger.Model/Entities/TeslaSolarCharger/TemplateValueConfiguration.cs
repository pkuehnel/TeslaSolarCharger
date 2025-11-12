using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class TemplateValueConfiguration
{
    public TemplateValueConfiguration(string name)
    {
        Name = name;
    }

    public int Id { get; set; }
    public string Name { get; set; }
    public int ConfigurationVersion { get; set; }
    public int? MinRefreshIntervalMilliseconds { get; set; }
    public TemplateValueGatherType GatherType { get; set; }
    public string? ConfigurationJson { get; set; }
}
