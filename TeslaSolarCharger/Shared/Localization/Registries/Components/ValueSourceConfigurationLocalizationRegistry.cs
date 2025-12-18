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
            new TextLocalizationTranslation(LanguageCodes.English, "Saved."),
            new TextLocalizationTranslation(LanguageCodes.German, "Gespeichert."));

        Register(TranslationKeys.ValueSourceConfigDeleted,
            new TextLocalizationTranslation(LanguageCodes.English, "Deleted."),
            new TextLocalizationTranslation(LanguageCodes.German, "Gelöscht."));

        Register(TranslationKeys.ValueSourceConfigCancel,
            new TextLocalizationTranslation(LanguageCodes.English, "Cancel"),
            new TextLocalizationTranslation(LanguageCodes.German, "Abbrechen"));

        Register(TranslationKeys.ValueSourceConfigTest,
            new TextLocalizationTranslation(LanguageCodes.English, "Test"),
            new TextLocalizationTranslation(LanguageCodes.German, "Testen"));

        Register(TranslationKeys.ValueSourceConfigProcessing,
            new TextLocalizationTranslation(LanguageCodes.English, "Processing"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wird verarbeitet"));

        Register(TranslationKeys.ValueSourceConfigAddResult,
            new TextLocalizationTranslation(LanguageCodes.English, "Add Result"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ergebnis hinzufügen"));

        Register(TranslationKeys.ValueSourceConfigAddHeader,
            new TextLocalizationTranslation(LanguageCodes.English, "Add Header"),
            new TextLocalizationTranslation(LanguageCodes.German, "Header hinzufügen"));

        Register(TranslationKeys.ValueSourceConfigHeaders,
            new TextLocalizationTranslation(LanguageCodes.English, "Headers"),
            new TextLocalizationTranslation(LanguageCodes.German, "Header"));

        Register(TranslationKeys.ValueSourceConfigTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} Value Configuration"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} Wertkonfiguration"));

        Register(TranslationKeys.ValueSourceConfigDeleteConfirm,
            new TextLocalizationTranslation(LanguageCodes.English, "Delete {0} Configuration"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0}-Konfiguration löschen"));

        Register(TranslationKeys.ValueSourceConfigDeleteConfigurationItem,
            new TextLocalizationTranslation(LanguageCodes.English, "this configuration item"),
            new TextLocalizationTranslation(LanguageCodes.German, "diesen Konfigurationseintrag"));

        Register(TranslationKeys.ValueSourceConfigConfigurationDeleted,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} configuration deleted"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0}-Konfiguration gelöscht"));

        Register(TranslationKeys.ValueSourceConfigNodePatternType,
            new TextLocalizationTranslation(LanguageCodes.English, "Node Pattern Type"),
            new TextLocalizationTranslation(LanguageCodes.German, "Knotenmustertyp"));

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
            new TextLocalizationTranslation(LanguageCodes.German, "Registertyp"));

        Register(TranslationKeys.ValueSourceConfigValueType,
            new TextLocalizationTranslation(LanguageCodes.English, "Value Type"),
            new TextLocalizationTranslation(LanguageCodes.German, "Werttyp"));

        Register(TranslationKeys.ValueSourceConfigEndianess,
            new TextLocalizationTranslation(LanguageCodes.English, "Endianess"),
            new TextLocalizationTranslation(LanguageCodes.German, "Byte-Reihenfolge"));

        Register(TranslationKeys.ValueSourceConfigHttpMethod,
            new TextLocalizationTranslation(LanguageCodes.English, "HTTP Method"),
            new TextLocalizationTranslation(LanguageCodes.German, "HTTP-Methode"));

        Register(TranslationKeys.ValueSourceConfigType,
            new TextLocalizationTranslation(LanguageCodes.English, "Type"),
            new TextLocalizationTranslation(LanguageCodes.German, "Typ"));

        Register(TranslationKeys.ValueSourceConfigFormNull,
            new TextLocalizationTranslation(LanguageCodes.English, "Config form is null, can not save values"),
            new TextLocalizationTranslation(LanguageCodes.German, "Konfigurationsformular ist null, Werte können nicht gespeichert werden"));

        Register(TranslationKeys.ValueSourceConfigConfigNull,
            new TextLocalizationTranslation(LanguageCodes.English, "Value configuration is null"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wertkonfiguration ist null"));

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
            new TextLocalizationTranslation(LanguageCodes.English, "Failed to update value configuration"),
            new TextLocalizationTranslation(LanguageCodes.German, "Aktualisieren der Wertkonfiguration fehlgeschlagen"));

        Register(TranslationKeys.ValueSourceConfigResultDeleteFailed,
            new TextLocalizationTranslation(LanguageCodes.English, "Failed to delete result configuration"),
            new TextLocalizationTranslation(LanguageCodes.German, "Löschen der Ergebniskonfiguration fehlgeschlagen"));

        Register(TranslationKeys.ValueSourceConfigResultDeleted,
            new TextLocalizationTranslation(LanguageCodes.English, "Result configuration deleted"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ergebniskonfiguration gelöscht"));

        Register(TranslationKeys.ValueSourceConfigCurrentRestStringFailed,
            new TextLocalizationTranslation(LanguageCodes.English, "Failed to get current rest string"),
            new TextLocalizationTranslation(LanguageCodes.German, "Abrufen des aktuellen REST-Strings fehlgeschlagen"));

        Register(TranslationKeys.ValueSourceConfigConfigurationValidationFailed,
            new TextLocalizationTranslation(LanguageCodes.English, "Configuration validation failed"),
            new TextLocalizationTranslation(LanguageCodes.German, "Konfigurationsvalidierung fehlgeschlagen"));

        Register(TranslationKeys.ModbusUrlUnitIdentifier,
            new TextLocalizationTranslation(LanguageCodes.English, "Unit Identifier"),
            new TextLocalizationTranslation(LanguageCodes.German, "Einheiten-ID"));

        Register(TranslationKeys.ModbusUrlRegisterType,
            new TextLocalizationTranslation(LanguageCodes.English, "Register Type"),
            new TextLocalizationTranslation(LanguageCodes.German, "Registertyp"));

        Register(TranslationKeys.ModbusUrlValueType,
            new TextLocalizationTranslation(LanguageCodes.English, "Value Type"),
            new TextLocalizationTranslation(LanguageCodes.German, "Werttyp"));

        Register(TranslationKeys.ModbusUrlRegisterAddress,
            new TextLocalizationTranslation(LanguageCodes.English, "Register Address"),
            new TextLocalizationTranslation(LanguageCodes.German, "Registeradresse"));

        Register(TranslationKeys.ModbusUrlQuantity,
            new TextLocalizationTranslation(LanguageCodes.English, "Number of Registers"),
            new TextLocalizationTranslation(LanguageCodes.German, "Anzahl der Register"));

        Register(TranslationKeys.ModbusUrlIpAddress,
            new TextLocalizationTranslation(LanguageCodes.English, "IP address"),
            new TextLocalizationTranslation(LanguageCodes.German, "IP-Adresse"));

        Register(TranslationKeys.ModbusUrlPort,
            new TextLocalizationTranslation(LanguageCodes.English, "Port"),
            new TextLocalizationTranslation(LanguageCodes.German, "Port"));

        Register(TranslationKeys.ModbusUrlSwapRegister,
            new TextLocalizationTranslation(LanguageCodes.English, "Swap Register (bigEndian / littleEndian):"),
            new TextLocalizationTranslation(LanguageCodes.German, "Register tauschen (bigEndian / littleEndian):"));

        Register(TranslationKeys.ModbusUrlConnectDelay,
            new TextLocalizationTranslation(LanguageCodes.English, "Connect Delay"),
            new TextLocalizationTranslation(LanguageCodes.German, "Verbindungsverzögerung"));

        Register(TranslationKeys.ModbusUrlReadTimeout,
            new TextLocalizationTranslation(LanguageCodes.English, "Read Timeout"),
            new TextLocalizationTranslation(LanguageCodes.German, "Lesezeitüberschreitung"));

        Register(TranslationKeys.NodePatternResultType,
            new TextLocalizationTranslation(LanguageCodes.English, "Result Type"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ergebnistyp"));

        Register(TranslationKeys.NodePatternJsonPattern,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} Json Pattern"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} JSON-Muster"));

        Register(TranslationKeys.NodePatternXmlPattern,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} XML Pattern"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} XML-Muster"));

        Register(TranslationKeys.NodePatternXmlHeaderName,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} XML Attribute Header Name"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} XML-Attribut Kopfzeilenname"));

        Register(TranslationKeys.NodePatternXmlHeaderValue,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} XML Attribute Header Value"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} XML-Attribut Kopfzeilenwert"));

        Register(TranslationKeys.NodePatternXmlValueName,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} XML Attribute Value Name"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} XML-Attribut Wertname"));
    }
}
