using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;

namespace TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries;

public class ModbusValueResultConfigurationPropertyLocalization : PropertyLocalizationRegistry<DtoModbusValueResultConfiguration>
{
    protected override void Configure()
    {
        Register(x => x.InvertedByModbusResultConfigurationId,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Inverted By Modbus Result Configuration",
                "If you have an inverter that always displays positive values, you can use this to invert the value based on a bit. For now this is only known for the battery power of Sungrow inverters"),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Invertiert durch Modbus-Ergebniskonfiguration",
                "Wenn Ihr Wechselrichter immer positive Werte anzeigt, können Sie hiermit den Wert anhand eines Bits invertieren. Aktuell ist dies nur für die Batterieleistung von Sungrow-Wechselrichtern bekannt."));

        Register(x => x.Address,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Address", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Adresse", null));

        Register(x => x.Length,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Length", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Länge", null));

        Register(x => x.BitStartIndex,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Bit Start Index", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Bit Start Index", null));

        Register(x => x.CorrectionFactor,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Correction Factor", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Korrekturfaktor", null));
    }
}
