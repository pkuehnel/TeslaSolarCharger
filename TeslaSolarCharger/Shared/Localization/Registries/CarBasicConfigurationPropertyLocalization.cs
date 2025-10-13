using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries;

public class CarBasicConfigurationPropertyLocalization : PropertyLocalizationRegistry<CarBasicConfiguration>
{
    protected override void Configure()
    {
        Register(x => x.MinimumAmpere,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Minimum Ampere",
                "TSC never sets a current below this value"),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Minimaler Strom",
                "TSC setzt den Strom niemals unter diesen Wert."));

        Register(x => x.MaximumAmpere,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Maximum Ampere",
                "TSC never sets a current above this value. This value is also used in the Max Power charge mode."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Maximaler Strom",
                "TSC setzt den Strom niemals über diesen Wert. Der Wert wird außerdem im Modus ‚Maximale Leistung‘ verwendet."));

        Register(x => x.SwitchOffAtCurrent,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Switch Off At Current",
                "The charging point will stop charging when the available current drops below this value. This allows charging to continue for a while even if the current dips slightly, preventing unnecessary interruptions. Note: If you set this value to e.g. 3A while Min Current is set to 6A, charging will continue with 6A as long as there is enough solar power for 3A."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Bei Stromstärke abschalten",
                "Der Ladepunkt beendet den Ladevorgang, wenn der verfügbare Strom unter diesen Wert fällt. So kann das Laden auch bei kurzfristigen Einbrüchen fortgesetzt werden und unnötige Unterbrechungen werden vermieden. Hinweis: Wenn Sie diesen Wert z. B. auf 3 A setzen, während der Mindeststrom 6 A beträgt, lädt das Fahrzeug weiter mit 6 A, solange ausreichend Solarstrom für 3 A vorhanden ist."));

        Register(x => x.SwitchOnAtCurrent,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Switch On At Current",
                "The charging point will only begin charging when the available current exceeds this value. Helps to avoid starting the charging process too frequently due to small current fluctuations."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Bei Stromstärke einschalten",
                "Der Ladepunkt beginnt erst mit dem Laden, wenn der verfügbare Strom diesen Wert überschreitet. Das verhindert häufige Startvorgänge durch kleine Stromschwankungen."));

        Register(x => x.UsableEnergy,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Usable Energy",
                "This value is used to reach a desired SoC in time if on spot price or PVOnly charge mode."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Nutzbare Energie",
                "Dieser Wert wird genutzt, um im Spotpreis- oder PV-Only-Modus rechtzeitig einen gewünschten SoC zu erreichen."));

        Register(x => x.ChargingPriority,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Charging Priority",
                "If there is not enough power for all cars/charging connectors, the cars/charging connectors will be charged ordered by priority. Cars/Charging connectors with the same priority are ordered randomly."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Ladepriorität",
                "Wenn nicht genug Leistung für alle Fahrzeuge/Ladepunkte vorhanden ist, werden sie entsprechend ihrer Priorität geladen. Fahrzeuge/Ladepunkte mit gleicher Priorität werden zufällig angeordnet."));

        Register(x => x.ShouldBeManaged,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Should Be Managed",
                "If disabled, this car will not show up in the overview page and TSC does not manage it."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Soll verwaltet werden",
                "Wenn deaktiviert, erscheint dieses Fahrzeug nicht in der Übersicht und wird von TSC nicht verwaltet."));

        Register(x => x.UseBle,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Use BLE",
                "Use BLE communication (If enabled no car license is required for this car). Note: A BLE device (e.g., Raspberry Pi) with installed TeslaSolarChargerBle Container needs to be near (max 4 meters without any walls in between) your car."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "BLE verwenden",
                "BLE-Kommunikation verwenden (wenn aktiviert, ist für dieses Fahrzeug keine Car-Lizenz erforderlich). Hinweis: Ein BLE-Gerät (z. B. Raspberry Pi) mit installiertem TeslaSolarChargerBle-Container muss sich in der Nähe des Fahrzeugs befinden (maximal 4 m ohne Wände dazwischen)."));

        Register(x => x.BleApiBaseUrl,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Ble Api Base Url",
                "Needed to send commands via BLE to the car. An example value would be `http://raspible:7210/`"),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "BLE-API-Basis-URL",
                "Wird benötigt, um Befehle per BLE an das Fahrzeug zu senden. Ein Beispielwert wäre `http://raspible:7210/`."));

        Register(x => x.UseFleetTelemetry,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Use Fleet Telemetry",
                "Only supported on cars with Software 2024.45.32+. Not supported on Pre 2021 Model S/X. If your car does not support fleet telemetry, this option will be disabled automatically within two minutes."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Flotten-Telemetrie verwenden",
                "Nur bei Fahrzeugen mit Software 2024.45.32+ verfügbar. Nicht unterstützt bei Model S/X vor 2021. Wenn Ihr Fahrzeug keine Flotten-Telemetrie unterstützt, wird diese Option innerhalb von zwei Minuten automatisch deaktiviert."));

        Register(x => x.IncludeTrackingRelevantFields,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Include Tracking Relevant Fields",
                "When enabled, TSC collects data of additional fields that are not necessarily required for TSC to work, but logged data might be helpful for future visualizations. Note: For this a car license is required."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Tracking-relevante Felder einbeziehen",
                "Wenn aktiviert, sammelt TSC zusätzliche Datenfelder, die für den Betrieb nicht zwingend erforderlich sind, aber für zukünftige Visualisierungen hilfreich sein können. Hinweis: Dafür ist eine Car-Lizenz erforderlich."));

        Register(x => x.MaximumPhases,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Maximum Phases",
                "Maximum number of phases the car can charge with. Used to calculate the maximum charging power and detect which car is connected to a charging connector"),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Maximale Phasen",
                "Maximale Anzahl an Phasen, mit denen das Fahrzeug laden kann. Wird genutzt, um die maximale Ladeleistung zu berechnen und das Fahrzeug an einem Ladepunkt zu identifizieren."));
    }
}
