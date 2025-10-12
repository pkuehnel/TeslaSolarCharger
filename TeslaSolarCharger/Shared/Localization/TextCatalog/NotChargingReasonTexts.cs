using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.TextCatalog;

public static class NotChargingReasonTexts
{
    public static LocalizedText SolarValuesTooOld { get; } =
        LocalizedTextFactory.Create("Solar values are too old", "Die Solarwerte sind zu alt");

    public static LocalizedText ChargingSpeedAdjustedDueToPowerBuffer { get; } =
        LocalizedTextFactory.Create("Charging speed is {0} due to power buffer being set to {1}W", "Die Ladegeschwindigkeit wird {0}, weil der Leistungspuffer auf {1} W gesetzt ist");

    public static LocalizedText Decreased { get; } =
        LocalizedTextFactory.Create("decreased", "verringert");

    public static LocalizedText Increased { get; } =
        LocalizedTextFactory.Create("increased", "erhöht");

    public static LocalizedText ReservedPowerForHomeBattery { get; } =
        LocalizedTextFactory.Create("Reserved {0}W for home battery charging as its SOC ({1}%) is below minimum SOC ({2}%)", "{0} W für das Laden der Heimbatterie reserviert, da ihr SoC ({1} %) unter dem Mindest-SoC ({2} %) liegt");

    public static LocalizedText CarIsFullyCharged { get; } =
        LocalizedTextFactory.Create("Car is fully charged", "Fahrzeug ist vollständig geladen");

    public static LocalizedText OcppConnectionNotEstablished { get; } =
        LocalizedTextFactory.Create("OCPP connection not established. After a TSC or charger reboot it can take up to 5 minutes until the charger is connected again.",
            "OCPP-Verbindung nicht hergestellt. Nach einem Neustart von TSC oder der Wallbox kann es bis zu 5 Minuten dauern, bis die Wallbox wieder verbunden ist.");

    public static LocalizedText CarIsNotAtHome { get; } =
        LocalizedTextFactory.Create("Car is not at home", "Das Fahrzeug befindet sich nicht zu Hause.");

    public static LocalizedText CarIsNotPluggedIn { get; } =
        LocalizedTextFactory.Create("Car is not plugged in", "Das Fahrzeug ist nicht eingesteckt.");

    public static LocalizedText ChargingConnectorIsNotPluggedIn { get; } =
        LocalizedTextFactory.Create("Charging connector is not plugged in", "Der Ladeanschluss ist nicht eingesteckt.");

    public static LocalizedText MaxCombinedCurrentTooLow { get; } =
        LocalizedTextFactory.Create("Charging stopped because of not enough max combined current.", "Ladevorgang gestoppt, da der maximale Gesamtstrom nicht ausreicht.");

    public static LocalizedText ChargeModeOffOrMaxSocReached { get; } =
        LocalizedTextFactory.Create("Charge mode is off or max SoC is reached.", "Der Lademodus ist deaktiviert oder das maximale SoC wurde erreicht.");

    public static LocalizedText MinOrMaxPhasesUnknown { get; } =
        LocalizedTextFactory.Create("Min Phases or Max Phases is unkown. Check the logs for further details.", "Die minimalen oder maximalen Phasen sind unbekannt. Bitte prüfe die Protokolle für weitere Details.");

    public static LocalizedText EstimatedVoltageUnknown { get; } =
        LocalizedTextFactory.Create("Estimated voltage while charging is unkown. Check the logs for further details.", "Die geschätzte Spannung während des Ladens ist unbekannt. Bitte prüfe die Protokolle für weitere Details.");

    public static LocalizedText WaitingForPhaseSwitchCooldown { get; } =
        LocalizedTextFactory.Create("Waiting phase switch cooldown time before starting to charge", "Warte auf die Abkühlzeit für den Phasenwechsel, bevor geladen wird.");

    public static LocalizedText WaitingForPhaseReduction { get; } =
        LocalizedTextFactory.Create("Waiting for phase reduction", "Warte auf Phasenreduzierung.");

    public static LocalizedText WaitingForPhaseIncrease { get; } =
        LocalizedTextFactory.Create("Waiting for phase increase", "Warte auf Phasenerhöhung.");

    public static LocalizedText WaitingForChargeStop { get; } =
        LocalizedTextFactory.Create("Waiting for charge stop", "Warte auf das Ende des Ladevorgangs.");

    public static LocalizedText ConfiguredMaxSocReached { get; } =
        LocalizedTextFactory.Create("Configured max Soc is reached", "Das konfigurierte maximale SoC wurde erreicht.");

    public static LocalizedText CarSocLimitReached { get; } =
        LocalizedTextFactory.Create("Car side SOC limit is reached. To start charging, the car side SOC limit needs to be at least {0}% higher than the actual SOC.",
            "Das fahrzeugseitige SoC-Limit wurde erreicht. Zum Starten des Ladevorgangs muss das fahrzeugseitige Limit mindestens {0}% über dem aktuellen SoC liegen.");

    public static LocalizedText ChargingStoppedByCar { get; } =
        LocalizedTextFactory.Create("Charging stopped by car, e.g. it is full or its charge limit is reached.", "Das Fahrzeug hat den Ladevorgang beendet, z. B. weil es voll ist oder das Limit erreicht wurde.");

    public static LocalizedText WaitingForChargeStart { get; } =
        LocalizedTextFactory.Create("Waiting for charge start", "Warte auf den Ladebeginn.");
}
