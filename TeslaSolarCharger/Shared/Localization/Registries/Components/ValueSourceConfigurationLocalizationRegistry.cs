using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

public class ValueSourceConfigurationLocalizationRegistry : TextLocalizationRegistry<ValueSourceConfigurationLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.ValueSourceConfigAdd,
            new TextLocalizationTranslation(LanguageCodes.English, "Add"),
            new TextLocalizationTranslation(LanguageCodes.German, "Hinzufügen"));

        Register(TranslationKeys.ValueSourceConfigEdit,
            new TextLocalizationTranslation(LanguageCodes.English, "Edit"),
            new TextLocalizationTranslation(LanguageCodes.German, "Bearbeiten"));

        Register(TranslationKeys.ValueSourceConfigDelete,
            new TextLocalizationTranslation(LanguageCodes.English, "Delete"),
            new TextLocalizationTranslation(LanguageCodes.German, "Löschen"));

        Register(TranslationKeys.ValueSourceConfigSave,
            new TextLocalizationTranslation(LanguageCodes.English, "Save"),
            new TextLocalizationTranslation(LanguageCodes.German, "Speichern"));

        Register(TranslationKeys.ValueSourceConfigSaved,
            new TextLocalizationTranslation(LanguageCodes.English, "Saved"),
            new TextLocalizationTranslation(LanguageCodes.German, "Gespeichert"));

        Register(TranslationKeys.ValueSourceConfigDeleted,
            new TextLocalizationTranslation(LanguageCodes.English, "Deleted"),
            new TextLocalizationTranslation(LanguageCodes.German, "Gelöscht"));

        Register(TranslationKeys.ValueSourceConfigCancel,
            new TextLocalizationTranslation(LanguageCodes.English, "Cancel"),
            new TextLocalizationTranslation(LanguageCodes.German, "Abbrechen"));

        Register(TranslationKeys.ValueSourceConfigTest,
            new TextLocalizationTranslation(LanguageCodes.English, "Test"),
            new TextLocalizationTranslation(LanguageCodes.German, "Testen"));

        Register(TranslationKeys.ValueSourceConfigProcessing,
            new TextLocalizationTranslation(LanguageCodes.English, "Processing"),
            new TextLocalizationTranslation(LanguageCodes.German, "Verarbeite"));

        Register(TranslationKeys.ValueSourceConfigAddResult,
            new TextLocalizationTranslation(LanguageCodes.English, "Add Result"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ergebnis hinzufügen"));

        Register(TranslationKeys.ValueSourceConfigAddHeader,
            new TextLocalizationTranslation(LanguageCodes.English, "Add Header"),
            new TextLocalizationTranslation(LanguageCodes.German, "Header hinzufügen"));

        Register(TranslationKeys.ValueSourceConfigHeaders,
            new TextLocalizationTranslation(LanguageCodes.English, "Headers"),
            new TextLocalizationTranslation(LanguageCodes.German, "Headers"));

        Register(TranslationKeys.ValueSourceConfigTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} config"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0}-Konfiguration"));

        Register(TranslationKeys.ValueSourceConfigDeleteConfirm,
            new TextLocalizationTranslation(LanguageCodes.English, "Delete {0} config?"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0}-Konfiguration löschen?"));

        Register(TranslationKeys.ValueSourceConfigDeleteConfigurationItem,
            new TextLocalizationTranslation(LanguageCodes.English, "the configuration"),
            new TextLocalizationTranslation(LanguageCodes.German, "die Konfiguration"));

        Register(TranslationKeys.ValueSourceConfigConfigurationDeleted,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} configuration deleted."),
            new TextLocalizationTranslation(LanguageCodes.German, "{0}-Konfiguration gelöscht."));

        Register(TranslationKeys.ValueSourceConfigNodePatternType,
            new TextLocalizationTranslation(LanguageCodes.English, "Node Pattern Type"),
            new TextLocalizationTranslation(LanguageCodes.German, "Node Pattern Typ"));

        Register(TranslationKeys.ValueSourceConfigUsedFor,
            new TextLocalizationTranslation(LanguageCodes.English, "Used for"),
            new TextLocalizationTranslation(LanguageCodes.German, "Verwendet für"));

        Register(TranslationKeys.ValueSourceConfigOperator,
            new TextLocalizationTranslation(LanguageCodes.English, "Operator"),
            new TextLocalizationTranslation(LanguageCodes.German, "Operator"));

        Register(TranslationKeys.ValueSourceConfigSolar,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solar"));

        Register(TranslationKeys.ValueSourceConfigRegisterType,
            new TextLocalizationTranslation(LanguageCodes.English, "Register Type"),
            new TextLocalizationTranslation(LanguageCodes.German, "Register-Typ"));

        Register(TranslationKeys.ValueSourceConfigValueType,
            new TextLocalizationTranslation(LanguageCodes.English, "Value Type"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wert-Typ"));

        Register(TranslationKeys.ValueSourceConfigEndianess,
            new TextLocalizationTranslation(LanguageCodes.English, "Endianess"),
            new TextLocalizationTranslation(LanguageCodes.German, "Endianess"));

        Register(TranslationKeys.ValueSourceConfigHttpMethod,
            new TextLocalizationTranslation(LanguageCodes.English, "HTTP Method"),
            new TextLocalizationTranslation(LanguageCodes.German, "HTTP-Methode"));

        Register(TranslationKeys.ValueSourceConfigType,
            new TextLocalizationTranslation(LanguageCodes.English, "Type"),
            new TextLocalizationTranslation(LanguageCodes.German, "Typ"));

        Register(TranslationKeys.ValueSourceConfigFormNull,
            new TextLocalizationTranslation(LanguageCodes.English, "Config form is null, can not save values"),
            new TextLocalizationTranslation(LanguageCodes.German, "Konfigurationsformular ist leer, Werte können nicht gespeichert werden"));

        Register(TranslationKeys.ValueSourceConfigConfigNull,
            new TextLocalizationTranslation(LanguageCodes.English, "Configuration is null"),
            new TextLocalizationTranslation(LanguageCodes.German, "Konfiguration ist leer"));

        Register(TranslationKeys.ValueSourceConfigConfigInvalid,
            new TextLocalizationTranslation(LanguageCodes.English, "Configuration is not valid"),
            new TextLocalizationTranslation(LanguageCodes.German, "Konfiguration ist ungültig"));

        Register(TranslationKeys.ValueSourceConfigResultRequired,
            new TextLocalizationTranslation(LanguageCodes.English, "At least one result configuration is required"),
            new TextLocalizationTranslation(LanguageCodes.German, "Mindestens eine Ergebniskonfiguration ist erforderlich"));

        Register(TranslationKeys.ValueSourceConfigResultInvalid,
            new TextLocalizationTranslation(LanguageCodes.English, "At least one result configuration is not valid"),
            new TextLocalizationTranslation(LanguageCodes.German, "Mindestens eine Ergebniskonfiguration ist ungültig"));

        Register(TranslationKeys.ValueSourceConfigUpdateFailed,
            new TextLocalizationTranslation(LanguageCodes.English, "Failed to update configuration"),
            new TextLocalizationTranslation(LanguageCodes.German, "Konfiguration konnte nicht aktualisiert werden"));

        Register(TranslationKeys.ValueSourceConfigResultDeleteFailed,
            new TextLocalizationTranslation(LanguageCodes.English, "Failed to delete result configuration"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ergebniskonfiguration konnte nicht gelöscht werden"));

        Register(TranslationKeys.ValueSourceConfigResultDeleted,
            new TextLocalizationTranslation(LanguageCodes.English, "Result configuration deleted"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ergebniskonfiguration gelöscht"));

        Register(TranslationKeys.ValueSourceConfigCurrentRestStringFailed,
            new TextLocalizationTranslation(LanguageCodes.English, "Failed to get current rest string"),
            new TextLocalizationTranslation(LanguageCodes.German, "Konnte aktuellen REST-String nicht abrufen"));

        Register(TranslationKeys.ValueSourceConfigConfigurationValidationFailed,
            new TextLocalizationTranslation(LanguageCodes.English, "Configuration validation failed"),
            new TextLocalizationTranslation(LanguageCodes.German, "Konfigurationsvalidierung fehlgeschlagen"));
    }
}
