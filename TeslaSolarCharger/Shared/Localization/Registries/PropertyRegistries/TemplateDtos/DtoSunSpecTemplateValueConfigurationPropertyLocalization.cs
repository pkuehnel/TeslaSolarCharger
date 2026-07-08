using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Generic;

namespace TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries.TemplateDtos;

public class DtoSunSpecTemplateValueConfigurationPropertyLocalization : PropertyLocalizationRegistry<DtoSunSpecTemplateValueConfiguration>
{
    protected override void Configure()
    {
        Register(x => x.EnableHomeBatteryControl,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Enable home battery control",
                "Allows TSC to block discharging and force charging of the home battery via the SunSpec storage model. The device must allow Modbus write access; some inverters require external battery control to be enabled in installer settings."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Heimspeichersteuerung aktivieren",
                "Erlaubt TSC, das Entladen des Heimspeichers zu blockieren und das Laden über das SunSpec-Speichermodell zu erzwingen. Das Gerät muss Modbus-Schreibzugriff erlauben; einige Wechselrichter erfordern die Aktivierung der externen Batteriesteuerung in den Installateureinstellungen."));

        Register(x => x.MaxChargeRatePercent,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Max charge rate",
                "Charge rate in percent of the inverter's maximum used when TSC forces the battery to charge."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Maximale Laderate",
                "Laderate in Prozent des Wechselrichter-Maximums, die verwendet wird, wenn TSC das Laden der Batterie erzwingt."));

        Register(x => x.MaxBatteryChargePowerW,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Max battery charge power",
                "Power in watts used when TSC forces the battery to charge."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Maximale Batterieladeleistung",
                "Leistung in Watt, die verwendet wird, wenn TSC das Laden der Batterie erzwingt."));
    }
}
