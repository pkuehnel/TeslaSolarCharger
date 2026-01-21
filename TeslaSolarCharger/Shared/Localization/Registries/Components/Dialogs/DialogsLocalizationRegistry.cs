using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.Dialogs;

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

        Register(TranslationKeys.TextDialogOk,
            new TextLocalizationTranslation(LanguageCodes.English, "OK"),
            new TextLocalizationTranslation(LanguageCodes.German, "OK"));
    }
}
