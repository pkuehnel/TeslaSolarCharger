using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Reasons;

public static class NotChargingWithExpectedPowerReasonLocalizationKeys
{
    public const string SolarValuesTooOld = nameof(SolarValuesTooOld);
    public const string ChargingSpeedDecreasedDueToPowerBuffer = nameof(ChargingSpeedDecreasedDueToPowerBuffer);
    public const string ChargingSpeedIncreasedDueToPowerBuffer = nameof(ChargingSpeedIncreasedDueToPowerBuffer);
    public const string ReservedPowerForHomeBatteryDueToLowSoc = nameof(ReservedPowerForHomeBatteryDueToLowSoc);
    public const string CarFullyCharged = nameof(CarFullyCharged);
}

public class NotChargingWithExpectedPowerReasonLocalizationRegistry : TextLocalizationRegistry<NotChargingWithExpectedPowerReasonLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(NotChargingWithExpectedPowerReasonLocalizationKeys.SolarValuesTooOld,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar values are too old"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solardaten sind zu alt"));

        Register(NotChargingWithExpectedPowerReasonLocalizationKeys.ChargingSpeedDecreasedDueToPowerBuffer,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging speed is decreased due to power buffer being set to {0}W"),
            new TextLocalizationTranslation(LanguageCodes.German, "Die Ladegeschwindigkeit ist reduziert, da der Leistungspuffer auf {0} W gesetzt ist"));

        Register(NotChargingWithExpectedPowerReasonLocalizationKeys.ChargingSpeedIncreasedDueToPowerBuffer,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging speed is increased due to power buffer being set to {0}W"),
            new TextLocalizationTranslation(LanguageCodes.German, "Die Ladegeschwindigkeit ist erhöht, da der Leistungspuffer auf {0} W gesetzt ist"));

        Register(NotChargingWithExpectedPowerReasonLocalizationKeys.ReservedPowerForHomeBatteryDueToLowSoc,
            new TextLocalizationTranslation(LanguageCodes.English, "Reserved {0}W for home battery charging as its state of charge ({1}%) is below the minimum ({2}%)"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} W für das Laden der Hausbatterie reserviert, da ihr Ladezustand ({1} %) unter dem Minimum ({2} %) liegt"));

        Register(NotChargingWithExpectedPowerReasonLocalizationKeys.CarFullyCharged,
            new TextLocalizationTranslation(LanguageCodes.English, "Car is fully charged"),
            new TextLocalizationTranslation(LanguageCodes.German, "Das Auto ist vollständig geladen"));
    }
}
