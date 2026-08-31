namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

public class DialogsLocalizationRegistry : TextLocalizationRegistry<DialogsLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.DeleteDialogContentFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Are you sure you want to delete {0}?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Sind Sie sicher, dass Sie {0} löschen möchten?"));

        Register(TranslationKeys.DeleteDialogCancel,
            new TextLocalizationTranslation(LanguageCodes.English, "Cancel"),
            new TextLocalizationTranslation(LanguageCodes.German, "Abbrechen"));

        Register(TranslationKeys.DeleteDialogConfirm,
            new TextLocalizationTranslation(LanguageCodes.English, "Yes"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ja"));

        Register(TranslationKeys.DeleteDialogConfirmationPromptFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "This will permanently delete all related data. Type \"{0}\" to confirm."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dadurch werden alle zugehörigen Daten unwiderruflich gelöscht. Geben Sie zur Bestätigung \"{0}\" ein."));

        Register(TranslationKeys.DeleteDialogConfirmationLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Name"),
            new TextLocalizationTranslation(LanguageCodes.German, "Name"));

        Register(TranslationKeys.TextDialogOk,
            new TextLocalizationTranslation(LanguageCodes.English, "OK"),
            new TextLocalizationTranslation(LanguageCodes.German, "OK"));
    }
}

