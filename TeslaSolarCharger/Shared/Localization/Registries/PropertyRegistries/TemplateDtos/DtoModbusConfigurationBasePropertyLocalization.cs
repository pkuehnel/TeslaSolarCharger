using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Generic;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Sma;

namespace TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries.TemplateDtos;

public class DtoModbusConfigurationBasePropertyLocalization : PropertyLocalizationRegistry<DtoModbusConfigurationBase>
{
    protected override void Configure()
    {
        Register(x => x.Host,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Host",
                "IP address or DNS name of your Modbus device"),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Host",
                "IP-Adresse oder DNS-Name deines Modbus Geräts"));
        Register(x => x.Port,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Modbus-Port",
                "The default value should not be changed normally"),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Modbus-Port",
                "Der Standardwert sollte normalerweise nicht geändert werden"));
        Register(x => x.UnitId,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Unit-ID",
                "The default value should not be changed normally"),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Unit-ID",
                "Der Standardwert sollte normalerweise nicht geändert werden"));

    }
}
