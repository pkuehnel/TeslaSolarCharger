using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos.TemplateConfiguration;

public interface ITemplateValueConfigurationDto
{
    int Id { get; set; }
    string Name { get; set; }
    int ConfigurationVersion { get; set; }
    int? MinRefreshIntervalMilliseconds { get; set; }
    TemplateValueGatherType GatherType { get; }
}
