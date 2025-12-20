using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class FleetApiTestComponentLocalizationRegistry : TextLocalizationRegistry<FleetApiTestComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.FleetApiTestLoading,
            new TextLocalizationTranslation(LanguageCodes.English, "Testing Fleet API access might take about 30 seconds..."),
            new TextLocalizationTranslation(LanguageCodes.German, "Das Testen des Fleet-API-Zugriffs kann etwa 30 Sekunden dauern..."));

        Register(TranslationKeys.FleetApiTestSuccess,
            new TextLocalizationTranslation(LanguageCodes.English, "API access is working."),
            new TextLocalizationTranslation(LanguageCodes.German, "API-Zugriff funktioniert."));

        Register(TranslationKeys.FleetApiTestFailed,
            new TextLocalizationTranslation(LanguageCodes.English, "API access is not working."),
            new TextLocalizationTranslation(LanguageCodes.German, "API-Zugriff funktioniert nicht."));

        Register(TranslationKeys.FleetApiTestFailedHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Note: For the test the car needs to be awake. If the car was not awake, wake it up and"),
            new TextLocalizationTranslation(LanguageCodes.German, "Hinweis: Für den Test muss das Auto wach sein. Wenn das Auto nicht wach war, wecken Sie es auf und"));

        Register(TranslationKeys.FleetApiTestAgainButton,
            new TextLocalizationTranslation(LanguageCodes.English, "test again"),
            new TextLocalizationTranslation(LanguageCodes.German, "erneut testen"));

        Register(TranslationKeys.FleetApiTestKeyCheckHint,
            new TextLocalizationTranslation(LanguageCodes.English, "If it still does not work, go to your car and under Controls -> Locks you can check if a key named \"solar4car.com\" is present. If not try adding the key again by clicking"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wenn es immer noch nicht funktioniert, gehen Sie zu Ihrem Auto und überprüfen Sie unter Fahrzeug -> Verriegelungen, ob ein Schlüssel namens \"solar4car.com\" vorhanden ist. Falls nicht, versuchen Sie, den Schlüssel erneut hinzuzufügen, indem Sie auf klicken"));

        Register(TranslationKeys.FleetApiTestHereLink,
            new TextLocalizationTranslation(LanguageCodes.English, "here"),
            new TextLocalizationTranslation(LanguageCodes.German, "hier"));

        Register(TranslationKeys.FleetApiTestNotTested,
            new TextLocalizationTranslation(LanguageCodes.English, "You did not test the Fleet API connection, yet. Wake up the car by opening a door, wait about 30 seconds and click"),
            new TextLocalizationTranslation(LanguageCodes.German, "Sie haben die Fleet-API-Verbindung noch nicht getestet. Wecken Sie das Auto auf, indem Sie eine Tür öffnen, warten Sie etwa 30 Sekunden und klicken Sie auf"));

        Register(TranslationKeys.FleetApiTestTestConnectionLinkSuffix,
            new TextLocalizationTranslation(LanguageCodes.English, "to test the connection."),
            new TextLocalizationTranslation(LanguageCodes.German, "um die Verbindung zu testen."));

        Register(TranslationKeys.FleetApiTestNotConfigured,
            new TextLocalizationTranslation(LanguageCodes.English, "TSC is not registered in car, click"),
            new TextLocalizationTranslation(LanguageCodes.German, "TSC ist nicht im Auto registriert, klicken Sie auf"));

        Register(TranslationKeys.FleetApiTestRegisterLink,
            new TextLocalizationTranslation(LanguageCodes.English, "to register the car."),
            new TextLocalizationTranslation(LanguageCodes.German, "um das Auto zu registrieren."));

        Register(TranslationKeys.FleetApiTestRegisterCarNote,
            new TextLocalizationTranslation(LanguageCodes.English, "Note: It is not possible to select the correct car. In order to register the correct car you need to select the car in the Tesla App before clicking on the button."),
            new TextLocalizationTranslation(LanguageCodes.German, "Hinweis: Es ist nicht möglich, das richtige Auto auszuwählen. Um das richtige Auto zu registrieren, müssen Sie das Auto in der Tesla App auswählen, bevor Sie auf die Schaltfläche klicken."));

        Register(TranslationKeys.FleetApiTestRegisteredButNotTested,
            new TextLocalizationTranslation(LanguageCodes.English, "You registered the car but did not test the connection yet. Click"),
            new TextLocalizationTranslation(LanguageCodes.German, "Sie haben das Auto registriert, aber die Verbindung noch nicht getestet. Klicken Sie auf"));

        Register(TranslationKeys.FleetApiTestStateLoadError,
            new TextLocalizationTranslation(LanguageCodes.English, "Could not load Tesla Fleet API state: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla Fleet API Status konnte nicht geladen werden: {0}"));
    }
}
