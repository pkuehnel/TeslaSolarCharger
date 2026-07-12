using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Generic;

namespace TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries.TemplateDtos;

public class DtoGenericModbusTemplateValueConfigurationPropertyLocalization : PropertyLocalizationRegistry<DtoGenericModbusTemplateValueConfiguration>
{
    protected override void Configure()
    {
        Register(x => x.EnableHomeBatteryControl,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Enable home battery control",
                "Allows TSC to block discharging and force charging of the home battery. The device must allow Modbus write access. Some vendors require a one time setup, see the documentation."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Heimspeichersteuerung aktivieren",
                "Erlaubt TSC, das Entladen des Heimspeichers zu blockieren und das Laden zu erzwingen. Das Gerät muss Modbus-Schreibzugriff erlauben. Einige Hersteller erfordern eine einmalige Einrichtung, siehe Dokumentation."));

        Register(x => x.MaxBatteryChargePowerW,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Max battery charge power",
                "Power in watts used when TSC forces the battery to charge. Must not exceed the battery's or inverter's maximum charge power."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Maximale Batterieladeleistung",
                "Leistung in Watt, die verwendet wird, wenn TSC das Laden der Batterie erzwingt. Darf die maximale Ladeleistung der Batterie bzw. des Wechselrichters nicht überschreiten."));

        Register(x => x.MaxBatteryDischargePowerW,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Max battery discharge power",
                "Discharge power limit in watts that is restored when TSC does not force a battery mode. Must not exceed the battery's or inverter's maximum discharge power."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Maximale Batterieentladeleistung",
                "Entladeleistungsgrenze in Watt, die wiederhergestellt wird, wenn TSC keinen Batteriemodus erzwingt. Darf die maximale Entladeleistung der Batterie bzw. des Wechselrichters nicht überschreiten."));
    }
}
