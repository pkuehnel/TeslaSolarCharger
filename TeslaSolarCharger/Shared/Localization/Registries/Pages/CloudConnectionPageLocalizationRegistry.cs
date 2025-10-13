using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class CloudConnectionPageLocalizationRegistry : TextLocalizationRegistry<CloudConnectionPageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register("Cloud Connection",
            new TextLocalizationTranslation(LanguageCodes.English, "Cloud Connection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Cloud-Verbindung"));

        Register("Solar4Car Backend connection",
            new TextLocalizationTranslation(LanguageCodes.English, "Solar4Car Backend connection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solar4Car-Backend-Verbindung"));

        Register("Logged in as unknown user",
            new TextLocalizationTranslation(LanguageCodes.English, "Logged in as unknown user"),
            new TextLocalizationTranslation(LanguageCodes.German, "Als unbekannter Benutzer angemeldet"));

        Register("Logged in as",
            new TextLocalizationTranslation(LanguageCodes.English, "Logged in as"),
            new TextLocalizationTranslation(LanguageCodes.German, "Angemeldet als"));

        Register("ChangeUser",
            new TextLocalizationTranslation(LanguageCodes.English, "ChangeUser"),
            new TextLocalizationTranslation(LanguageCodes.German, "Benutzer wechseln"));

        Register("Login failed:",
            new TextLocalizationTranslation(LanguageCodes.English, "Login failed:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Anmeldung fehlgeschlagen:"));

        Register("Login",
            new TextLocalizationTranslation(LanguageCodes.English, "Login"),
            new TextLocalizationTranslation(LanguageCodes.German, "Anmelden"));

        Register("Register",
            new TextLocalizationTranslation(LanguageCodes.English, "Register"),
            new TextLocalizationTranslation(LanguageCodes.German, "Registrieren"));

        Register("Tesla Fleet API Connection",
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla Fleet API Connection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla-Fleet-API-Verbindung"));

        Register("Request Token",
            new TextLocalizationTranslation(LanguageCodes.English, "Request Token"),
            new TextLocalizationTranslation(LanguageCodes.German, "Token anfordern"));

        Register("Could not check Token state. Is your TSC connected to the internet?",
            new TextLocalizationTranslation(LanguageCodes.English, "Could not check Token state. Is your TSC connected to the internet?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tokenstatus konnte nicht geprüft werden. Ist dein TSC mit dem Internet verbunden?"));

        Register("No Token found, login below to get a Backend Token",
            new TextLocalizationTranslation(LanguageCodes.English, "No Token found, login below to get a Backend Token"),
            new TextLocalizationTranslation(LanguageCodes.German, "Kein Token gefunden, melde dich unten an, um ein Backend-Token zu erhalten"));

        Register("Your Backend Token is unauthorized. Reasons could be a changed Solar4Car.com password, a second TSC running with the same installation ID (displayed on the home page at the very bottom) or your TSC was not running for quite a while.",
            new TextLocalizationTranslation(LanguageCodes.English, "Your Backend Token is unauthorized. Reasons could be a changed Solar4Car.com password, a second TSC running with the same installation ID (displayed on the home page at the very bottom) or your TSC was not running for quite a while."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dein Backend-Token ist nicht autorisiert. Gründe können ein geändertes Solar4Car.com-Passwort, ein zweiter TSC mit derselben Installations-ID (angezeigt ganz unten auf der Startseite) oder ein längerer Ausfall deines TSC sein."));

        Register("Your Backend Token has missing scopes",
            new TextLocalizationTranslation(LanguageCodes.English, "Your Backend Token has missing scopes"),
            new TextLocalizationTranslation(LanguageCodes.German, "Deinem Backend-Token fehlen Berechtigungen"));

        Register("Your Backend Token is expired, which means it could not be refreshed automatically. Reasons could be a changed Solar4Car.com password, a second TSC running with the same installation ID (displayed on the home page at the very bottom) or your TSC was not running for quite a while.",
            new TextLocalizationTranslation(LanguageCodes.English, "Your Backend Token is expired, which means it could not be refreshed automatically. Reasons could be a changed Solar4Car.com password, a second TSC running with the same installation ID (displayed on the home page at the very bottom) or your TSC was not running for quite a while."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dein Backend-Token ist abgelaufen und konnte nicht automatisch erneuert werden. Gründe können ein geändertes Solar4Car.com-Passwort, ein zweiter TSC mit derselben Installations-ID (angezeigt ganz unten auf der Startseite) oder ein längerer Ausfall deines TSC sein."));

        Register("You are connected to the backend, everything is working as expected.",
            new TextLocalizationTranslation(LanguageCodes.English, "You are connected to the backend, everything is working as expected."),
            new TextLocalizationTranslation(LanguageCodes.German, "Du bist mit dem Backend verbunden, alles funktioniert wie erwartet."));

        Register("A login to solar4car.com is required before requesting a Tesla Fleet API Token.",
            new TextLocalizationTranslation(LanguageCodes.English, "A login to solar4car.com is required before requesting a Tesla Fleet API Token."),
            new TextLocalizationTranslation(LanguageCodes.German, "Eine Anmeldung bei solar4car.com ist erforderlich, bevor du ein Tesla-Fleet-API-Token anfordern kannst."));

        Register("You did not request a Fleet API Token, yet. Request a new token, allow access to all scopes and enable mobile access in your car.",
            new TextLocalizationTranslation(LanguageCodes.English, "You did not request a Fleet API Token, yet. Request a new token, allow access to all scopes and enable mobile access in your car."),
            new TextLocalizationTranslation(LanguageCodes.German, "Du hast noch kein Fleet-API-Token angefordert. Fordere ein neues Token an, gewähre Zugriff auf alle Berechtigungen und aktiviere den mobilen Zugriff in deinem Auto."));

        Register("Your token is unauthorized. Request a new token, allow access to all scopes and enable mobile access in your car.",
            new TextLocalizationTranslation(LanguageCodes.English, "Your token is unauthorized. Request a new token, allow access to all scopes and enable mobile access in your car."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dein Token ist nicht autorisiert. Fordere ein neues Token an, gewähre Zugriff auf alle Berechtigungen und aktiviere den mobilen Zugriff in deinem Auto."));

        Register("Your token has missing scopes. Request a new Token and allow all scopes (only required scopes are requested).",
            new TextLocalizationTranslation(LanguageCodes.English, "Your token has missing scopes. Request a new Token and allow all scopes (only required scopes are requested)."),
            new TextLocalizationTranslation(LanguageCodes.German, "Deinem Token fehlen Berechtigungen. Fordere ein neues Token an und erlaube alle Berechtigungen (es werden nur benötigte Berechtigungen angefordert)."));

        Register("Your Fleet API token is expired. Request a new Token and allow all scopes (only required scopes are requested).",
            new TextLocalizationTranslation(LanguageCodes.English, "Your Fleet API token is expired. Request a new Token and allow all scopes (only required scopes are requested)."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dein Fleet-API-Token ist abgelaufen. Fordere ein neues Token an und erlaube alle Berechtigungen (es werden nur benötigte Berechtigungen angefordert)."));

        Register("Everything is fine! If you want to generate a new token e.g. to switch to another Tesla Account please click the button below:",
            new TextLocalizationTranslation(LanguageCodes.English, "Everything is fine! If you want to generate a new token e.g. to switch to another Tesla Account please click the button below:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Alles in Ordnung! Wenn du ein neues Token generieren möchtest, z. B. um zu einem anderen Tesla-Konto zu wechseln, klicke auf den Button unten:"));

        Register("Login did not succeed",
            new TextLocalizationTranslation(LanguageCodes.English, "Login did not succeed"),
            new TextLocalizationTranslation(LanguageCodes.German, "Anmeldung war nicht erfolgreich"));

        Register("Login succeeded",
            new TextLocalizationTranslation(LanguageCodes.English, "Login succeeded"),
            new TextLocalizationTranslation(LanguageCodes.German, "Anmeldung erfolgreich"));

        Register("You need to be logged in to Solar4Car.com to generate a Fleet API Token",
            new TextLocalizationTranslation(LanguageCodes.English, "You need to be logged in to Solar4Car.com to generate a Fleet API Token"),
            new TextLocalizationTranslation(LanguageCodes.German, "Du musst bei Solar4Car.com angemeldet sein, um ein Fleet-API-Token zu generieren"));

        Register("Could not generate Tesla Login URL",
            new TextLocalizationTranslation(LanguageCodes.English, "Could not generate Tesla Login URL"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla-Login-URL konnte nicht erstellt werden"));
    }
}
