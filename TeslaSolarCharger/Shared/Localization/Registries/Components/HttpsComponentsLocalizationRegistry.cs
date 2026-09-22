namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

/// <summary>
/// Texts of the HTTPS hint on the home page and the HTTPS section of the base configuration. Install steps are
/// separated by line breaks, each line is one step.
/// </summary>
public class HttpsComponentsLocalizationRegistry : TextLocalizationRegistry<HttpsComponentsLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.HttpsHintTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Faster and secure: use HTTPS"),
            new TextLocalizationTranslation(LanguageCodes.German, "Schneller und sicher: HTTPS verwenden"));

        Register(TranslationKeys.HttpsHintText,
            new TextLocalizationTranslation(LanguageCodes.English, "This page is open without HTTPS. Without HTTPS, Safari on iPhone, iPad and Mac (from version 27) runs Solar4Car up to ten times slower, and browsers mark the page as not secure. Install the certificate on this device once, then use the secure address."),
            new TextLocalizationTranslation(LanguageCodes.German, "Diese Seite ist ohne HTTPS geöffnet. Ohne HTTPS läuft Solar4Car in Safari auf iPhone, iPad und Mac (ab Version 27) bis zu zehnmal langsamer, und Browser markieren die Seite als nicht sicher. Installieren Sie das Zertifikat einmalig auf diesem Gerät und verwenden Sie dann die sichere Adresse."));

        Register(TranslationKeys.HttpsDownloadCertificateButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Download certificate"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zertifikat herunterladen"));

        Register(TranslationKeys.HttpsOpenSecureAddressButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Open secure address"),
            new TextLocalizationTranslation(LanguageCodes.German, "Sichere Adresse öffnen"));

        Register(TranslationKeys.HttpsSectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "HTTPS"),
            new TextLocalizationTranslation(LanguageCodes.German, "HTTPS"));

        Register(TranslationKeys.HttpsSectionDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar4Car creates its own certificate for HTTPS and adds every host name and IP address it is opened with. Install the certificate on each device you use Solar4Car on, so the browser trusts the secure address without a warning."),
            new TextLocalizationTranslation(LanguageCodes.German, "Solar4Car erstellt ein eigenes Zertifikat für HTTPS und nimmt jeden Hostnamen und jede IP-Adresse auf, unter der es geöffnet wird. Installieren Sie das Zertifikat auf jedem Gerät, auf dem Sie Solar4Car verwenden, damit der Browser der sicheren Adresse ohne Warnung vertraut."));

        Register(TranslationKeys.HttpsEnabledStatus,
            new TextLocalizationTranslation(LanguageCodes.English, "HTTPS is available on port {0}."),
            new TextLocalizationTranslation(LanguageCodes.German, "HTTPS ist auf Port {0} verfügbar."));

        Register(TranslationKeys.HttpsDisabledStatus,
            new TextLocalizationTranslation(LanguageCodes.English, "HTTPS is not available: either it is switched off with the environment variable HttpsPort=0, or its port is already in use (see the logs)."),
            new TextLocalizationTranslation(LanguageCodes.German, "HTTPS ist nicht verfügbar: Entweder ist es mit der Umgebungsvariable HttpsPort=0 abgeschaltet, oder sein Port ist bereits belegt (siehe Logs)."));

        Register(TranslationKeys.HttpsCoveredNamesLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "The certificate is valid for:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Das Zertifikat gilt für:"));

        Register(TranslationKeys.HttpsRootCertificateLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Certificate to install:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zu installierendes Zertifikat:"));

        Register(TranslationKeys.HttpsFingerprintLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "SHA-256 fingerprint:"),
            new TextLocalizationTranslation(LanguageCodes.German, "SHA-256-Fingerabdruck:"));

        Register(TranslationKeys.HttpsValidUntilLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Valid until:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Gültig bis:"));

        Register(TranslationKeys.HttpsInstallInstructionsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "How to install the certificate"),
            new TextLocalizationTranslation(LanguageCodes.German, "So installieren Sie das Zertifikat"));

        Register(TranslationKeys.HttpsInstallIosTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "iPhone and iPad"),
            new TextLocalizationTranslation(LanguageCodes.German, "iPhone und iPad"));

        Register(TranslationKeys.HttpsInstallIosSteps,
            new TextLocalizationTranslation(LanguageCodes.English,
                "Tap \"Download certificate\" and allow the configuration profile to be downloaded.\n"
                + "Open Settings, tap \"Profile Downloaded\" and install the profile.\n"
                + "Go to Settings > General > About > Certificate Trust Settings and turn on the Solar4Car certificate.\n"
                + "Open the secure address."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Tippen Sie auf „Zertifikat herunterladen“ und erlauben Sie das Laden des Konfigurationsprofils.\n"
                + "Öffnen Sie die Einstellungen, tippen Sie auf „Profil geladen“ und installieren Sie das Profil.\n"
                + "Öffnen Sie Einstellungen > Allgemein > Info > Zertifikatsvertrauenseinstellungen und aktivieren Sie das Solar4Car-Zertifikat.\n"
                + "Öffnen Sie die sichere Adresse."));

        Register(TranslationKeys.HttpsInstallAndroidTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Android"),
            new TextLocalizationTranslation(LanguageCodes.German, "Android"));

        Register(TranslationKeys.HttpsInstallAndroidSteps,
            new TextLocalizationTranslation(LanguageCodes.English,
                "Tap \"Download certificate\".\n"
                + "Open Settings and search for \"CA certificate\" (usually under Security > More security settings > Install from device storage).\n"
                + "Choose \"CA certificate\", confirm the warning and select the downloaded Solar4Car-root.cer.\n"
                + "Open the secure address."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Tippen Sie auf „Zertifikat herunterladen“.\n"
                + "Öffnen Sie die Einstellungen und suchen Sie nach „CA-Zertifikat“ (meist unter Sicherheit > Weitere Sicherheitseinstellungen > Vom Gerätespeicher installieren).\n"
                + "Wählen Sie „CA-Zertifikat“, bestätigen Sie die Warnung und wählen Sie die heruntergeladene Datei Solar4Car-root.cer.\n"
                + "Öffnen Sie die sichere Adresse."));

        Register(TranslationKeys.HttpsInstallWindowsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Windows"),
            new TextLocalizationTranslation(LanguageCodes.German, "Windows"));

        Register(TranslationKeys.HttpsInstallWindowsSteps,
            new TextLocalizationTranslation(LanguageCodes.English,
                "Click \"Download certificate\" and open the downloaded file.\n"
                + "Click \"Install Certificate...\", choose \"Current User\", then \"Place all certificates in the following store\" and select \"Trusted Root Certification Authorities\".\n"
                + "Finish the wizard and restart the browser. Firefox asks right after the download instead: tick \"Trust this CA to identify websites\".\n"
                + "Open the secure address."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Klicken Sie auf „Zertifikat herunterladen“ und öffnen Sie die heruntergeladene Datei.\n"
                + "Klicken Sie auf „Zertifikat installieren...“, wählen Sie „Aktueller Benutzer“, dann „Alle Zertifikate in folgendem Speicher speichern“ und wählen Sie „Vertrauenswürdige Stammzertifizierungsstellen“.\n"
                + "Schließen Sie den Assistenten ab und starten Sie den Browser neu. Firefox fragt stattdessen direkt nach dem Download: Setzen Sie den Haken bei „Dieser CA vertrauen, um Websites zu identifizieren“.\n"
                + "Öffnen Sie die sichere Adresse."));

        Register(TranslationKeys.HttpsInstallMacTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Mac"),
            new TextLocalizationTranslation(LanguageCodes.German, "Mac"));

        Register(TranslationKeys.HttpsInstallMacSteps,
            new TextLocalizationTranslation(LanguageCodes.English,
                "Click \"Download certificate\" and open the downloaded file, Keychain Access adds it.\n"
                + "In Keychain Access, double-click the Solar4Car certificate, open \"Trust\" and set \"When using this certificate\" to \"Always Trust\".\n"
                + "Close the window, confirm with your password and restart the browser.\n"
                + "Open the secure address."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Klicken Sie auf „Zertifikat herunterladen“ und öffnen Sie die heruntergeladene Datei, die Schlüsselbundverwaltung fügt sie hinzu.\n"
                + "Doppelklicken Sie in der Schlüsselbundverwaltung auf das Solar4Car-Zertifikat, öffnen Sie „Vertrauen“ und setzen Sie „Bei Verwendung dieses Zertifikats“ auf „Immer vertrauen“.\n"
                + "Schließen Sie das Fenster, bestätigen Sie mit Ihrem Passwort und starten Sie den Browser neu.\n"
                + "Öffnen Sie die sichere Adresse."));
    }
}
