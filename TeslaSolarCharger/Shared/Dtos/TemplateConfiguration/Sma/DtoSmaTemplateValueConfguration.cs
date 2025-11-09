using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Sma;

public class DtoSmaTemplateValueConfguration
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 502;
    public int UnitId { get; set; } = 3;
}


public class DtoSmaInverterTemplateValueConfiguration
    : DtoTemplateValueConfiguration<DtoSmaTemplateValueConfguration>
{
    public override TemplateValueGatherType GatherType => TemplateValueGatherType.SmaInverterModbus;
}

public class DtoSmaHybridInverterTemplateValueConfiguration
    : DtoTemplateValueConfiguration<DtoSmaTemplateValueConfguration>
{
    public override TemplateValueGatherType GatherType => TemplateValueGatherType.SmaHybridInverterModbus;
}
