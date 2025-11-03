using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class FleetApiTestComponentLocalizationRegistry : TextLocalizationRegistry<FleetApiTestComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.CouldNotLoadTeslaFleetApi,
            new TextLocalizationTranslation(LanguageCodes.English, "Could not load Tesla Fleet API state: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla Fleet API-Status konnte nicht geladen werden: {0}"));

        Register(TranslationKeys.TestingFleetApiAccessMightTake,
            new TextLocalizationTranslation(LanguageCodes.English, "Testing Fleet API access might take about 30 seconds..."),
            new TextLocalizationTranslation(LanguageCodes.German, "Der Test des Tesla Fleet API-Zugriffs kann etwa 30 Sekunden dauern..."));

        Register(TranslationKeys.ApiAccessIsWorking,
            new TextLocalizationTranslation(LanguageCodes.English, "API access is working."),
            new TextLocalizationTranslation(LanguageCodes.German, "API-Zugriff funktioniert."));

        Register(TranslationKeys.ApiAccessIsNotWorking,
            new TextLocalizationTranslation(LanguageCodes.English, "API access is not working."),
            new TextLocalizationTranslation(LanguageCodes.German, "API-Zugriff funktioniert nicht."));

        Register(TranslationKeys.NoteForTheTestTheCar,
            new TextLocalizationTranslation(LanguageCodes.English, "Note: For the test the car needs to be awake. If the car was not awake, wake it up and"),
            new TextLocalizationTranslation(LanguageCodes.German, "Hinweis: Für den Test muss das Fahrzeug wach sein. Falls das Fahrzeug nicht wach war, wecke es und"));

        Register(TranslationKeys.TestAgain,
            new TextLocalizationTranslation(LanguageCodes.English, "test again"),
            new TextLocalizationTranslation(LanguageCodes.German, "erneut testen"));

        Register(TranslationKeys.IfItStillDoesNotWork,
            new TextLocalizationTranslation(LanguageCodes.English, "If it still does not work, go to your car and under Controls -> Locks you can check if a key named \"solar4car.com\" is present. If not try adding the key again by clicking"),
            new TextLocalizationTranslation(LanguageCodes.German, "Falls es weiterhin nicht funktioniert, gehe zu deinem Fahrzeug und prüfe unter Steuerungen -> Verriegelungen, ob ein Schlüssel mit dem Namen \"solar4car.com\" vorhanden ist. Falls nicht, füge den Schlüssel erneut hinzu, indem du"));

        Register(TranslationKeys.Here,
            new TextLocalizationTranslation(LanguageCodes.English, "here"),
            new TextLocalizationTranslation(LanguageCodes.German, "hier"));

        Register(TranslationKeys.YouDidNotTestTheFleet,
            new TextLocalizationTranslation(LanguageCodes.English, "You did not test the Fleet API connection, yet. Wake up the car by opening a door, wait about 30 seconds and click"),
            new TextLocalizationTranslation(LanguageCodes.German, "Du hast die Fleet-API-Verbindung noch nicht getestet. Öffne eine Tür, warte etwa 30 Sekunden und klicke"));

        Register(TranslationKeys.ToTestTheConnection,
            new TextLocalizationTranslation(LanguageCodes.English, "to test the connection."),
            new TextLocalizationTranslation(LanguageCodes.German, "um die Verbindung zu testen."));

        Register(TranslationKeys.TscIsNotRegisteredInCar,
            new TextLocalizationTranslation(LanguageCodes.English, "TSC is not registered in car, click"),
            new TextLocalizationTranslation(LanguageCodes.German, "TSC ist nicht im Fahrzeug registriert, klicke"));

        Register(TranslationKeys.ToRegisterTheCar,
            new TextLocalizationTranslation(LanguageCodes.English, "to register the car."),
            new TextLocalizationTranslation(LanguageCodes.German, "um das Fahrzeug zu registrieren."));

        Register(TranslationKeys.NoteItIsNotPossibleTo,
            new TextLocalizationTranslation(LanguageCodes.English, "Note: It is not possible to select the correct car. In order to register the correct car you need to select the car in the Tesla App before clicking on the button."),
            new TextLocalizationTranslation(LanguageCodes.German, "Hinweis: Es ist nicht möglich, im Dialog das richtige Fahrzeug auszuwählen. Wähle das Fahrzeug daher vorher in der Tesla-App aus, bevor du auf den Button klickst."));

        Register(TranslationKeys.YouRegisteredTheCarButDid,
            new TextLocalizationTranslation(LanguageCodes.English, "You registered the car but did not test the connection yet. Click"),
            new TextLocalizationTranslation(LanguageCodes.German, "Du hast das Fahrzeug registriert, aber die Verbindung noch nicht getestet. Klicke"));
    }
}
