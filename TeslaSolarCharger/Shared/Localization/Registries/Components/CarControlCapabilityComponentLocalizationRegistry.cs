using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

public class CarControlCapabilityComponentLocalizationRegistry : TextLocalizationRegistry<CarControlCapabilityComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.CarControlCapabilityTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "How TeslaSolarCharger controls your car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wie TeslaSolarCharger Ihr Fahrzeug steuert"));

        Register(TranslationKeys.CarControlCapabilityIntro,
            new TextLocalizationTranslation(LanguageCodes.English, "To charge smartly, TeslaSolarCharger needs two things: a way to control charging (start/stop and charging speed), and a way to read your car's battery level. Here is what each connection can do:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zum intelligenten Laden benötigt TeslaSolarCharger zwei Dinge: eine Möglichkeit, das Laden zu steuern (Start/Stopp und Ladegeschwindigkeit), und eine Möglichkeit, den Ladestand Ihres Fahrzeugs auszulesen. Folgendes kann jede Verbindung:"));

        Register(TranslationKeys.CarControlCapabilityColConnection,
            new TextLocalizationTranslation(LanguageCodes.English, "Connection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Verbindung"));

        Register(TranslationKeys.CarControlCapabilityColControl,
            new TextLocalizationTranslation(LanguageCodes.English, "Start/stop & charging speed"),
            new TextLocalizationTranslation(LanguageCodes.German, "Start/Stopp & Ladegeschwindigkeit"));

        Register(TranslationKeys.CarControlCapabilityColBattery,
            new TextLocalizationTranslation(LanguageCodes.English, "Battery level (SoC)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestand (SoC)"));

        Register(TranslationKeys.CarControlCapabilityColCost,
            new TextLocalizationTranslation(LanguageCodes.English, "Cost"),
            new TextLocalizationTranslation(LanguageCodes.German, "Kosten"));

        Register(TranslationKeys.CarControlCapabilityColWorksWith,
            new TextLocalizationTranslation(LanguageCodes.English, "Works with"),
            new TextLocalizationTranslation(LanguageCodes.German, "Funktioniert mit"));

        Register(TranslationKeys.CarControlCapabilityRowChargingStation,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging station (OCPP)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestation (OCPP)"));

        Register(TranslationKeys.CarControlCapabilityRowTeslaBle,
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla – Bluetooth (BLE)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla – Bluetooth (BLE)"));

        Register(TranslationKeys.CarControlCapabilityRowTeslaFleetApi,
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla – Fleet API"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla – Fleet-API"));

        Register(TranslationKeys.CarControlCapabilityCostFree,
            new TextLocalizationTranslation(LanguageCodes.English, "Free"),
            new TextLocalizationTranslation(LanguageCodes.German, "Kostenlos"));

        Register(TranslationKeys.CarControlCapabilityCostFleetApi,
            new TextLocalizationTranslation(LanguageCodes.English, "€2.99 / month"),
            new TextLocalizationTranslation(LanguageCodes.German, "2,99 € / Monat"));

        Register(TranslationKeys.CarControlCapabilityWorksAnyCar,
            new TextLocalizationTranslation(LanguageCodes.English, "Any car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Jedes Fahrzeug"));

        Register(TranslationKeys.CarControlCapabilityWorksTeslas,
            new TextLocalizationTranslation(LanguageCodes.English, "Teslas"),
            new TextLocalizationTranslation(LanguageCodes.German, "Teslas"));

        Register(TranslationKeys.CarControlCapabilityFootnote,
            new TextLocalizationTranslation(LanguageCodes.English, "Other brands: a charging station controls charging; reading the battery level is optionally available via Smartcar (requires a license)."),
            new TextLocalizationTranslation(LanguageCodes.German, "Andere Marken: Eine Ladestation steuert das Laden; das Auslesen des Ladestands ist optional über Smartcar verfügbar (lizenzpflichtig)."));

        Register(TranslationKeys.CarControlCapabilityYes,
            new TextLocalizationTranslation(LanguageCodes.English, "Yes"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ja"));

        Register(TranslationKeys.CarControlCapabilityNo,
            new TextLocalizationTranslation(LanguageCodes.English, "No"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nein"));
    }
}
