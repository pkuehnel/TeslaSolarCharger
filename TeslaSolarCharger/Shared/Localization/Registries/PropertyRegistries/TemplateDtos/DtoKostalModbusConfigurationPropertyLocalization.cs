using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Kostal;

namespace TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries.TemplateDtos;

public class DtoKostalModbusConfigurationPropertyLocalization : PropertyLocalizationRegistry<DtoKostalModbusConfiguration>
{
    protected override void Configure()
    {
        Register(x => x.Host,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Host or IP",
                "The hostname or IP address of the Kostal Inverter."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Host oder IP",
                "Der Hostname oder die IP-Adresse des Kostal Wechselrichters."));

        Register(x => x.Port,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Port",
                "The Modbus TCP port (Default: 1502 for Kostal)."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Port",
                "Der Modbus-TCP-Port (Standard: 1502 bei Kostal)."));

        Register(x => x.UnitId,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Unit ID",
                "The Modbus Unit ID (Default: 71 for Kostal)."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Unit ID",
                "Die Modbus Unit-ID (Standard: 71 bei Kostal)."));
    }
}
