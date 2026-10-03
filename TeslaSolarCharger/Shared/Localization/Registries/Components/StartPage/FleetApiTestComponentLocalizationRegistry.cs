using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class FleetApiTestComponentLocalizationRegistry : TextLocalizationRegistry<FleetApiTestComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.FleetApiTestLoading,
            new TextLocalizationTranslation(LanguageCodes.English, "Testing the Tesla cloud access might take about 30 seconds..."),
            new TextLocalizationTranslation(LanguageCodes.German, "Das Testen des Tesla-Cloud-Zugriffs kann etwa 30 Sekunden dauern..."));

        Register(TranslationKeys.FleetApiTestSuccess,
            new TextLocalizationTranslation(LanguageCodes.English, "The connection via the Tesla cloud works."),
            new TextLocalizationTranslation(LanguageCodes.German, "Die Verbindung über die Tesla-Cloud funktioniert."));

        Register(TranslationKeys.FleetApiTestFailed,
            new TextLocalizationTranslation(LanguageCodes.English, "The connection via the Tesla cloud does not work yet."),
            new TextLocalizationTranslation(LanguageCodes.German, "Die Verbindung über die Tesla-Cloud funktioniert noch nicht."));

        Register(TranslationKeys.FleetApiTestNotTested,
            new TextLocalizationTranslation(LanguageCodes.English, "The connection via the Tesla cloud has not been tested yet."),
            new TextLocalizationTranslation(LanguageCodes.German, "Die Verbindung über die Tesla-Cloud wurde noch nicht getestet."));

        Register(TranslationKeys.FleetApiTestNotConfigured,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar4Car is not registered in this car yet."),
            new TextLocalizationTranslation(LanguageCodes.German, "Solar4Car ist in diesem Fahrzeug noch nicht registriert."));

        Register(TranslationKeys.FleetApiTestNotWorking,
            new TextLocalizationTranslation(LanguageCodes.English, "The car did not accept Solar4Car the last time we tried."),
            new TextLocalizationTranslation(LanguageCodes.German, "Das Fahrzeug hat Solar4Car beim letzten Versuch nicht akzeptiert."));

        Register(TranslationKeys.FleetApiTestRegisteredButNotTested,
            new TextLocalizationTranslation(LanguageCodes.English, "You added the key but have not tested the connection yet."),
            new TextLocalizationTranslation(LanguageCodes.German, "Sie haben den Schlüssel hinzugefügt, aber die Verbindung noch nicht getestet."));

        Register(TranslationKeys.FleetApiTestOptionalExplanation,
            new TextLocalizationTranslation(LanguageCodes.English, "Recommended, but optional: if Bluetooth cannot reach the car, Solar4Car sends the command via the Tesla cloud instead. Without a Car License for this car, this fallback is limited to one command per hour."),
            new TextLocalizationTranslation(LanguageCodes.German, "Empfohlen, aber optional: Wenn Bluetooth das Fahrzeug nicht erreicht, sendet Solar4Car den Befehl stattdessen über die Tesla-Cloud. Ohne Fahrzeuglizenz für dieses Fahrzeug ist diese Ausweichlösung auf einen Befehl pro Stunde begrenzt."));

        Register(TranslationKeys.FleetApiTestStepAddKeyTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "1. Add the Solar4Car key to your car"),
            new TextLocalizationTranslation(LanguageCodes.German, "1. Den Solar4Car-Schlüssel zum Fahrzeug hinzufügen"));

        Register(TranslationKeys.FleetApiTestStepAddKeyText,
            new TextLocalizationTranslation(LanguageCodes.English, "First select this car in the Tesla app on your phone: Tesla adds the key to the car that is selected there. Then click the button and confirm the key \"solar4car.com\" in the Tesla app."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wählen Sie zuerst dieses Fahrzeug in der Tesla-App auf Ihrem Smartphone aus: Tesla fügt den Schlüssel dem dort ausgewählten Fahrzeug hinzu. Klicken Sie dann auf die Schaltfläche und bestätigen Sie den Schlüssel \"solar4car.com\" in der Tesla-App."));

        Register(TranslationKeys.FleetApiTestAddKeyButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Add key"),
            new TextLocalizationTranslation(LanguageCodes.German, "Schlüssel hinzufügen"));

        Register(TranslationKeys.FleetApiTestStepTestTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "2. Test the connection"),
            new TextLocalizationTranslation(LanguageCodes.German, "2. Die Verbindung testen"));

        Register(TranslationKeys.FleetApiTestStepTestText,
            new TextLocalizationTranslation(LanguageCodes.English, "The car has to be awake for the test: open a door, wait about 30 seconds, then start the test."),
            new TextLocalizationTranslation(LanguageCodes.German, "Für den Test muss das Fahrzeug wach sein: Öffnen Sie eine Tür, warten Sie etwa 30 Sekunden und starten Sie dann den Test."));

        Register(TranslationKeys.FleetApiTestTestButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Test connection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Verbindung testen"));

        Register(TranslationKeys.FleetApiTestKeyCheckHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Still not working? In the car, check under Controls -> Locks whether a key named \"solar4car.com\" is listed. If it is not, add it again with step 1."),
            new TextLocalizationTranslation(LanguageCodes.German, "Funktioniert es immer noch nicht? Prüfen Sie im Fahrzeug unter Steuerung -> Verriegelungen, ob ein Schlüssel namens \"solar4car.com\" aufgeführt ist. Falls nicht, fügen Sie ihn mit Schritt 1 erneut hinzu."));

        Register(TranslationKeys.FleetApiTestStateLoadError,
            new TextLocalizationTranslation(LanguageCodes.English, "Could not load Tesla cloud state: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla-Cloud-Status konnte nicht geladen werden: {0}"));
    }
}
