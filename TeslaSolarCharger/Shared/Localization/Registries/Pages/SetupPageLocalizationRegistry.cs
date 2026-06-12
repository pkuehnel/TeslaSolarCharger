using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class SetupPageLocalizationRegistry : TextLocalizationRegistry<SetupPageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.SetupAssistantTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Setup Assistant"),
            new TextLocalizationTranslation(LanguageCodes.German, "Einrichtungs-Assistent"));

        Register(TranslationKeys.SetupStepWelcome,
            new TextLocalizationTranslation(LanguageCodes.English, "Welcome"),
            new TextLocalizationTranslation(LanguageCodes.German, "Willkommen"));

        Register(TranslationKeys.SetupStepLocation,
            new TextLocalizationTranslation(LanguageCodes.English, "Location"),
            new TextLocalizationTranslation(LanguageCodes.German, "Standort"));

        Register(TranslationKeys.SetupStepSolarBattery,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar & Battery"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solar & Batterie"));

        Register(TranslationKeys.SetupStepCloudConnection,
            new TextLocalizationTranslation(LanguageCodes.English, "Cloud Connection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Cloud-Verbindung"));

        Register(TranslationKeys.SetupStepPrices,
            new TextLocalizationTranslation(LanguageCodes.English, "Electricity Prices"),
            new TextLocalizationTranslation(LanguageCodes.German, "Strompreise"));

        Register(TranslationKeys.SetupStepCars,
            new TextLocalizationTranslation(LanguageCodes.English, "Cars"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeuge"));

        Register(TranslationKeys.SetupStepChargingStations,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Stations"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestationen"));

        Register(TranslationKeys.SetupStepFinish,
            new TextLocalizationTranslation(LanguageCodes.English, "Finish"),
            new TextLocalizationTranslation(LanguageCodes.German, "Abschluss"));

        Register(TranslationKeys.SetupWelcomeTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Welcome to TeslaSolarCharger!"),
            new TextLocalizationTranslation(LanguageCodes.German, "Willkommen bei TeslaSolarCharger!"));

        Register(TranslationKeys.SetupWelcomeDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "This assistant will guide you through the basic configuration to get you started as quickly as possible."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dieser Assistent führt Sie durch die Basiskonfiguration, um Ihnen den Einstieg so einfach wie möglich zu machen."));

        Register(TranslationKeys.SetupChangeLaterInfo,
            new TextLocalizationTranslation(LanguageCodes.English, "You can always change these settings later in the configuration pages."),
            new TextLocalizationTranslation(LanguageCodes.German, "Sie können diese Einstellungen jederzeit später in den Konfigurationsseiten ändern."));

        Register(TranslationKeys.SetupHasPvSystemQuestion,
            new TextLocalizationTranslation(LanguageCodes.English, "Do you have a photovoltaic (PV) system?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Haben Sie eine Photovoltaikanlage (PV)?"));

        Register(TranslationKeys.SetupLocationTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "System Location"),
            new TextLocalizationTranslation(LanguageCodes.German, "Standort der Anlage"));

        Register(TranslationKeys.SetupLocationDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "Please set the location of your PV system on the map. This is used for solar power predictions. For certain vehicle models, this location is also used to determine whether the vehicle is at home."),
            new TextLocalizationTranslation(LanguageCodes.German, "Bitte legen Sie den Standort Ihrer PV-Anlage auf der Karte fest. Dies wird für Solarstromprognosen verwendet. Bei bestimmten Fahrzeugmodellen wird dieser Standort außerdem verwendet, um festzustellen, ob sich das Fahrzeug zu Hause befindet."));

        Register(TranslationKeys.SetupSolarBatteryTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar & Battery Data"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solar- & Batteriedaten"));

        Register(TranslationKeys.SetupSolarBatteryDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "Configure how TeslaSolarCharger gets your current solar production and grid usage data. Templates are the easiest way to start."),
            new TextLocalizationTranslation(LanguageCodes.German, "Konfigurieren Sie, wie TeslaSolarCharger Ihre aktuelle Solarproduktion und den Netzbezug ermittelt. Vorlagen sind der einfachste Weg zum Starten."));

        Register(TranslationKeys.SetupPvNotAvailableAsTemplate,
            new TextLocalizationTranslation(LanguageCodes.English, "My PV system is not available as a template"),
            new TextLocalizationTranslation(LanguageCodes.German, "Meine PV-Anlage ist nicht als Vorlage verfügbar"));

        Register(TranslationKeys.SetupHasHomeBatteryQuestion,
            new TextLocalizationTranslation(LanguageCodes.English, "Do you have a home battery?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Haben Sie eine Heimbatterie?"));

        Register(TranslationKeys.SetupHomeBatterySettingsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Home Battery Settings"),
            new TextLocalizationTranslation(LanguageCodes.German, "Heimbatterie-Einstellungen"));

        Register(TranslationKeys.SetupCloudConnectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar4Car Cloud Connection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solar4Car-Cloud-Verbindung"));

        Register(TranslationKeys.SetupCloudConnectionDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "A Solar4Car account is required to use TeslaSolarCharger. Please log in or create an account to continue. You cannot proceed until your instance is connected."),
            new TextLocalizationTranslation(LanguageCodes.German, "Für die Nutzung von TeslaSolarCharger ist ein Solar4Car-Konto erforderlich. Bitte melden Sie sich an oder erstellen Sie ein Konto, um fortzufahren. Sie können erst fortfahren, wenn Ihre Instanz verbunden ist."));

        Register(TranslationKeys.SetupCloudConnectionRequiredNotification,
            new TextLocalizationTranslation(LanguageCodes.English, "Please connect your instance to the Solar4Car cloud before continuing."),
            new TextLocalizationTranslation(LanguageCodes.German, "Bitte verbinden Sie Ihre Instanz mit der Solar4Car-Cloud, bevor Sie fortfahren."));

        Register(TranslationKeys.SetupPricesTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Electricity Prices"),
            new TextLocalizationTranslation(LanguageCodes.German, "Strompreise"));

        Register(TranslationKeys.SetupPricesDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "Configure your electricity prices to allow TeslaSolarCharger to charge when it's cheapest."),
            new TextLocalizationTranslation(LanguageCodes.German, "Konfigurieren Sie Ihre Strompreise, damit TeslaSolarCharger laden kann, wenn es am günstigsten ist."));

        Register(TranslationKeys.SetupCarsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Cars"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeuge"));

        Register(TranslationKeys.SetupLicenseInfoTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "License Information"),
            new TextLocalizationTranslation(LanguageCodes.German, "Lizenzinformationen"));

        Register(TranslationKeys.SetupLicenseInfoContent,
            new TextLocalizationTranslation(LanguageCodes.English, "A license is required for all API-connected vehicles to enable smart charging features."),
            new TextLocalizationTranslation(LanguageCodes.German, "Für alle über API angebundenen Fahrzeuge ist eine Lizenz erforderlich, um intelligente Ladefunktionen zu aktivieren."));

        Register(TranslationKeys.SetupLicenseInfoException,
            new TextLocalizationTranslation(LanguageCodes.English, "Exception: Teslas connected directly via Bluetooth (BLE) do not require a separate license."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ausnahme: Teslas, die direkt über Bluetooth (BLE) verbunden sind, benötigen keine separate Lizenz."));

        Register(TranslationKeys.SetupChargingStationsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Stations"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestationen"));

        Register(TranslationKeys.SetupChargingStationsDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "As soon as a charging station connects via OCPP, it will appear here in real-time."),
            new TextLocalizationTranslation(LanguageCodes.German, "Sobald sich eine Ladestation über OCPP verbindet, erscheint sie hier in Echtzeit."));

        Register(TranslationKeys.SetupNoStationsConnected,
            new TextLocalizationTranslation(LanguageCodes.English, "No charging stations connected yet..."),
            new TextLocalizationTranslation(LanguageCodes.German, "Noch keine Ladestationen verbunden..."));

        Register(TranslationKeys.SetupFinishTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Almost done!"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fast fertig!"));

        Register(TranslationKeys.SetupFinishDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "Click \"Finish Setup\" to save your configuration and start using TeslaSolarCharger."),
            new TextLocalizationTranslation(LanguageCodes.German, "Klicken Sie auf \"Einrichtung abschließen\", um Ihre Konfiguration zu speichern und TeslaSolarCharger zu nutzen."));

        Register(TranslationKeys.SetupSuccessInfo,
            new TextLocalizationTranslation(LanguageCodes.English, "Your initial setup is complete. You can always revisit the detailed settings pages for fine-tuning."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihre Ersteinrichtung ist abgeschlossen. Sie können die detaillierten Einstellungsseiten jederzeit für die Feinabstimmung besuchen."));

        Register(TranslationKeys.SetupFinishButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Finish Setup"),
            new TextLocalizationTranslation(LanguageCodes.German, "Einrichtung abschließen"));

        Register(TranslationKeys.SetupSuccessNotification,
            new TextLocalizationTranslation(LanguageCodes.English, "Setup completed successfully!"),
            new TextLocalizationTranslation(LanguageCodes.German, "Einrichtung erfolgreich abgeschlossen!"));
    }
}
