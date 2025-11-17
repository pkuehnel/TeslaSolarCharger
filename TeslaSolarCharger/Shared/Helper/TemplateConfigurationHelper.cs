using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Sma;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Solax;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Helper;

public static class TemplateValueConfigurationTypeHelper
{
    // Map GatherType -> Config type
    private static readonly Dictionary<TemplateValueGatherType, Type> GatherTypeToConfigType
        = new()
        {
            { TemplateValueGatherType.SmaEnergyMeter,           typeof(DtoSmaEnergyMeterTemplateValueConfiguration) },
            { TemplateValueGatherType.SmaInverterModbus,        typeof(DtoSmaInverterTemplateValueConfiguration) },
            { TemplateValueGatherType.SmaHybridInverterModbus,  typeof(DtoSmaInverterTemplateValueConfiguration) },
            //{ TemplateValueGatherType.TeslaPowerwallFleetApi,   typeof(DtoTeslaPowerwallFleetTemplateValueConfiguration) },
            { TemplateValueGatherType.SolaxApi,   typeof(DtoSolaxTemplateValueConfiguration) },
        };

    public static Type? GetConfigurationType(TemplateValueGatherType gatherType)
    {
        return GatherTypeToConfigType.GetValueOrDefault(gatherType);
    }
}
