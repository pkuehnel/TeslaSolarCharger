using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Fronius;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Generic;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Kostal;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Sma;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Solax;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.TeslaPowerwall;
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
            { TemplateValueGatherType.TeslaPowerwallFleetApi,   typeof(DtoTeslaPowerwallTemplateValueConfiguration) },
            { TemplateValueGatherType.SolaxApi,   typeof(DtoSolaxTemplateValueConfiguration) },
            { TemplateValueGatherType.KostalHybridInverterModbus,   typeof(DtoKostalModbusConfiguration) },
            { TemplateValueGatherType.FroniusSolarApiV1,   typeof(DtoFroniusSolarApiTemplateValueConfiguration) },
        };

    public static Type? GetConfigurationType(TemplateValueGatherType gatherType)
    {
        if (GenericModbusTemplateSettings.IsGenericModbusType(gatherType))
        {
            return typeof(DtoGenericModbusTemplateValueConfiguration);
        }
        return GatherTypeToConfigType.GetValueOrDefault(gatherType);
    }
}
