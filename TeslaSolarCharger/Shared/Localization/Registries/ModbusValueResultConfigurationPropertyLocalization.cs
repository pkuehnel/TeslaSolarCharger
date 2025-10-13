using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries;

public class ModbusValueResultConfigurationPropertyLocalization : PropertyLocalizationRegistry<DtoModbusValueResultConfiguration>
{
    protected override void Configure()
    {
        Register(x => x.InvertedByModbusResultConfigurationId,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                null,
                "If you have an inverter that always displays positive values, you can use this to invert the value based on a bit. For now this is only known for the battery power of Sungrow inverters"),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                null,
                "Wenn Ihr Wechselrichter immer positive Werte anzeigt, können Sie hiermit den Wert anhand eines Bits invertieren. Aktuell ist dies nur für die Batterieleistung von Sungrow-Wechselrichtern bekannt."));
    }
}
