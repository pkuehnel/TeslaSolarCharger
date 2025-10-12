using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.TextCatalog;

public static class ChargingCostTexts
{
    public static LocalizedText StartTime { get; } =
        LocalizedTextFactory.CreateForProperty<DtoHandledCharge>(c => c.StartTime, "Start time", "Startzeit");

    public static LocalizedText EndTime { get; } =
        LocalizedTextFactory.CreateForProperty<DtoHandledCharge>(c => c.EndTime, "End time", "Endzeit");

    public static LocalizedText CalculatedPrice { get; } =
        LocalizedTextFactory.CreateForProperty<DtoHandledCharge>(c => c.CalculatedPrice, "Calculated price", "Berechneter Preis");

    public static LocalizedText PricePerKwh { get; } =
        LocalizedTextFactory.CreateForProperty<DtoHandledCharge>(c => c.PricePerKwh, "Price per kWh", "Preis pro kWh");

    public static LocalizedText UsedGridEnergy { get; } =
        LocalizedTextFactory.CreateForProperty<DtoHandledCharge>(c => c.UsedGridEnergy, "Used grid energy", "Verbrauchte Netzenergie");

    public static LocalizedText UsedHomeBatteryEnergy { get; } =
        LocalizedTextFactory.CreateForProperty<DtoHandledCharge>(c => c.UsedHomeBatteryEnergy, "Used home battery energy", "Verbrauchte Heimbatterie-Energie");

    public static LocalizedText UsedSolarEnergy { get; } =
        LocalizedTextFactory.CreateForProperty<DtoHandledCharge>(c => c.UsedSolarEnergy, "Used solar energy", "Verbrauchte Solarenergie");
}
