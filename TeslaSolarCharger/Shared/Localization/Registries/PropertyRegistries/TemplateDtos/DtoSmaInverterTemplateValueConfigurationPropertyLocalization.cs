using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Sma;

namespace TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries.TemplateDtos;

public class DtoSmaInverterTemplateValueConfigurationPropertyLocalization : PropertyLocalizationRegistry<DtoSmaInverterTemplateValueConfiguration>
{
    protected override void Configure()
    {
        Register(x => x.EnableHomeBatteryControl,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Enable home battery control",
                "Allows TSC to block discharging and force charging of the home battery. The Modbus TCP server of the inverter must allow write access."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Heimspeichersteuerung aktivieren",
                "Erlaubt TSC, das Entladen des Heimspeichers zu blockieren und das Laden zu erzwingen. Der Modbus-TCP-Server des Wechselrichters muss Schreibzugriff erlauben."));

        Register(x => x.MaxBatteryChargePowerW,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Max battery charge power",
                "Power in watts used when TSC forces the battery to charge."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Maximale Batterieladeleistung",
                "Leistung in Watt, die verwendet wird, wenn TSC das Laden der Batterie erzwingt."));

        Register(x => x.MaxBatteryDischargePowerW,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Max battery discharge power",
                "Discharge power limit in watts that is restored when TSC does not force a battery mode."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Maximale Batterieentladeleistung",
                "Entladeleistungsgrenze in Watt, die wiederhergestellt wird, wenn TSC keinen Batteriemodus erzwingt."));
    }
}
