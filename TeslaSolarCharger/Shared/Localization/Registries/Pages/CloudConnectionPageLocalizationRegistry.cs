using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class CloudConnectionPageLocalizationRegistry : TextLocalizationRegistry<CloudConnectionPageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.CloudConnectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Cloud Connection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Cloud-Verbindung"));

        Register(TranslationKeys.CloudConnectionBackendSectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar4Car Cloud connection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solar4Car-Cloud-Verbindung"));

        Register(TranslationKeys.CloudConnectionLoggedInAsUnknownUser,
            new TextLocalizationTranslation(LanguageCodes.English, "Logged in as unknown user"),
            new TextLocalizationTranslation(LanguageCodes.German, "Als unbekannter Benutzer angemeldet"));

        Register(TranslationKeys.CloudConnectionLoggedInAsPrefix,
            new TextLocalizationTranslation(LanguageCodes.English, "Logged in as"),
            new TextLocalizationTranslation(LanguageCodes.German, "Angemeldet als"));

        Register(TranslationKeys.CloudConnectionChangeUserButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Change user"),
            new TextLocalizationTranslation(LanguageCodes.German, "Benutzer wechseln"));

        Register(TranslationKeys.CloudConnectionLoginFailedPrefix,
            new TextLocalizationTranslation(LanguageCodes.English, "Login failed:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Anmeldung fehlgeschlagen:"));

        Register(TranslationKeys.CloudConnectionLoginButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Login"),
            new TextLocalizationTranslation(LanguageCodes.German, "Anmelden"));

        Register(TranslationKeys.CloudConnectionRegisterLink,
            new TextLocalizationTranslation(LanguageCodes.English, "Register"),
            new TextLocalizationTranslation(LanguageCodes.German, "Registrieren"));

        Register(TranslationKeys.CloudConnectionTeslaFleetApiSectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla Fleet API Connection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla-Fleet-API-Verbindung"));

        Register(TranslationKeys.CloudConnectionRequestTokenButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Request Token"),
            new TextLocalizationTranslation(LanguageCodes.German, "Token anfordern"));

        Register(TranslationKeys.CloudConnectionTokenStateMissingPrecondition,
            new TextLocalizationTranslation(LanguageCodes.English, "Could not check Token state. Is your TSC connected to the internet?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tokenstatus konnte nicht geprüft werden. Ist Ihr TSC mit dem Internet verbunden?"));

        Register(TranslationKeys.CloudConnectionTokenStateNotAvailable,
            new TextLocalizationTranslation(LanguageCodes.English, "No Token found, login below to get a Backend Token"),
            new TextLocalizationTranslation(LanguageCodes.German, "Kein Token gefunden, melden Sie sich unten an, um ein Backend-Token zu erhalten"));

        Register(TranslationKeys.CloudConnectionTokenStateUnauthorized,
            new TextLocalizationTranslation(LanguageCodes.English, "Your Backend Token is unauthorized. Reasons could be a changed Solar4Car.com password, a second TSC running with the same installation ID (displayed on the home page at the very bottom) or your TSC was not running for quite a while."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihr Backend-Token ist nicht autorisiert. Gründe können ein geändertes Solar4Car.com-Passwort, ein zweiter TSC mit derselben Installations-ID (angezeigt ganz unten auf der Startseite) oder ein längerer Ausfall Ihres TSC sein."));

        Register(TranslationKeys.CloudConnectionTokenStateMissingScopes,
            new TextLocalizationTranslation(LanguageCodes.English, "Your Backend Token has missing scopes"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihrem Backend-Token fehlen Berechtigungen"));

        Register(TranslationKeys.CloudConnectionTokenStateExpired,
            new TextLocalizationTranslation(LanguageCodes.English, "Your Backend Token is expired, which means it could not be refreshed automatically. Reasons could be a changed Solar4Car.com password, a second TSC running with the same installation ID (displayed on the home page at the very bottom) or your TSC was not running for quite a while."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihr Backend-Token ist abgelaufen und konnte nicht automatisch erneuert werden. Gründe können ein geändertes Solar4Car.com-Passwort, ein zweiter TSC mit derselben Installations-ID (angezeigt ganz unten auf der Startseite) oder ein längerer Ausfall Ihres TSC sein."));

        Register(TranslationKeys.CloudConnectionTokenStateUpToDate,
            new TextLocalizationTranslation(LanguageCodes.English, "You are connected to the backend, everything is working as expected."),
            new TextLocalizationTranslation(LanguageCodes.German, "Sie sind mit dem Backend verbunden, alles funktioniert wie erwartet."));

        Register(TranslationKeys.CloudConnectionFleetApiTokenStateMissingPrecondition,
            new TextLocalizationTranslation(LanguageCodes.English, "A login to solar4car.com is required before requesting a Tesla Fleet API Token."),
            new TextLocalizationTranslation(LanguageCodes.German, "Eine Anmeldung bei solar4car.com ist erforderlich, bevor Sie ein Tesla-Fleet-API-Token anfordern können."));

        Register(TranslationKeys.CloudConnectionFleetApiTokenStateNotAvailable,
            new TextLocalizationTranslation(LanguageCodes.English, "You did not request a Fleet API Token, yet. Request a new token, allow access to all scopes and enable mobile access in your car."),
            new TextLocalizationTranslation(LanguageCodes.German, "Sie haben noch kein Fleet-API-Token angefordert. Fordern Sie ein neues Token an, gewähren Sie Zugriff auf alle Berechtigungen und aktivieren Sie den mobilen Zugriff in Ihrem Auto."));

        Register(TranslationKeys.CloudConnectionFleetApiTokenStateUnauthorized,
            new TextLocalizationTranslation(LanguageCodes.English, "Your token is unauthorized. Request a new token, allow access to all scopes and enable mobile access in your car."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihr Token ist nicht autorisiert. Fordern Sie ein neues Token an, gewähren Sie Zugriff auf alle Berechtigungen und aktivieren Sie den mobilen Zugriff in Ihrem Auto."));

        Register(TranslationKeys.CloudConnectionFleetApiTokenStateMissingScopes,
            new TextLocalizationTranslation(LanguageCodes.English, "Your token has missing scopes. Request a new Token and allow all scopes (only required scopes are requested)."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihrem Token fehlen Berechtigungen. Fordern Sie ein neues Token an und erlauben Sie alle Berechtigungen (es werden nur benötigte Berechtigungen angefordert)."));

        Register(TranslationKeys.CloudConnectionFleetApiTokenStateExpired,
            new TextLocalizationTranslation(LanguageCodes.English, "Your Fleet API token is expired. Request a new Token and allow all scopes (only required scopes are requested)."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihr Fleet-API-Token ist abgelaufen. Fordern Sie ein neues Token an und erlauben Sie alle Berechtigungen (es werden nur benötigte Berechtigungen angefordert)."));

        Register(TranslationKeys.CloudConnectionFleetApiTokenStateUpToDate,
            new TextLocalizationTranslation(LanguageCodes.English, "Everything is fine! If you want to generate a new token e.g. to switch to another Tesla Account please click the button below:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Alles in Ordnung! Wenn Sie ein neues Token generieren möchten, z. B. um zu einem anderen Tesla-Konto zu wechseln, klicken Sie auf den Button unten:"));

        Register(TranslationKeys.CloudConnectionLoginFailedNotification,
            new TextLocalizationTranslation(LanguageCodes.English, "Login did not succeed"),
            new TextLocalizationTranslation(LanguageCodes.German, "Anmeldung war nicht erfolgreich"));

        Register(TranslationKeys.CloudConnectionLoginSucceededNotification,
            new TextLocalizationTranslation(LanguageCodes.English, "Login succeeded"),
            new TextLocalizationTranslation(LanguageCodes.German, "Anmeldung erfolgreich"));

        Register(TranslationKeys.CloudConnectionFleetApiLoginRequirementNotification,
            new TextLocalizationTranslation(LanguageCodes.English, "You need to be logged in to Solar4Car.com to generate a Fleet API Token"),
            new TextLocalizationTranslation(LanguageCodes.German, "Sie müssen bei Solar4Car.com angemeldet sein, um ein Fleet-API-Token zu generieren"));

        Register(TranslationKeys.CloudConnectionTeslaLoginUrlGenerationError,
            new TextLocalizationTranslation(LanguageCodes.English, "Could not generate Tesla Login URL"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla-Login-URL konnte nicht erstellt werden"));
    }
}
