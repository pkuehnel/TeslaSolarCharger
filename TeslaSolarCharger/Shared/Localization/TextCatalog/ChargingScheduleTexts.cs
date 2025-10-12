using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.TextCatalog;

public static class ChargingScheduleTexts
{
    public static LocalizedText ValidFrom { get; } =
        LocalizedTextFactory.CreateForProperty<ValidFromToBase>(b => b.ValidFrom, "Valid from", "Gültig ab");

    public static LocalizedText ValidTo { get; } =
        LocalizedTextFactory.CreateForProperty<ValidFromToBase>(b => b.ValidTo, "Valid to", "Gültig bis");

    public static LocalizedText CarId { get; } =
        LocalizedTextFactory.CreateForProperty<DtoChargingSchedule>(s => s.CarId, "Car", "Fahrzeug");

    public static LocalizedText OcppChargingConnectorId { get; } =
        LocalizedTextFactory.CreateForProperty<DtoChargingSchedule>(s => s.OcppChargingConnectorId, "Charging connector", "Ladeanschluss");

    public static LocalizedText OnlyChargeOnAtLeastSolarPower { get; } =
        LocalizedTextFactory.CreateForProperty<DtoChargingSchedule>(s => s.OnlyChargeOnAtLeastSolarPower, "Minimum solar power for charging", "Mindest-Solarleistung für das Laden");

    public static LocalizedText ChargingPower { get; } =
        LocalizedTextFactory.CreateForProperty<DtoChargingSchedule>(s => s.ChargingPower, "Charging power", "Ladeleistung");

    public static LocalizedText TargetGridPower { get; } =
        LocalizedTextFactory.CreateForProperty<DtoChargingSchedule>(s => s.TargetGridPower, "Target grid power", "Ziel-Netzleistung");
}
