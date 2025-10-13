using TeslaSolarCharger.Shared.Dtos.ChargingStation;
using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries;

public class ChargingStationConnectorPropertyLocalization : PropertyLocalizationRegistry<DtoChargingStationConnector>
{
    protected override void Configure()
    {
        Register(x => x.ShouldBeManaged,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Show connector on homepage",
                "Note: This auto reenables as soon as the charging connector is connected. To permanently disable a charging connector remove the OCPP configuration in your Wallbox."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Stecker auf der Startseite anzeigen",
                "Hinweis: Diese Option wird automatisch wieder aktiviert, sobald der Ladeanschluss verbunden ist. Um einen Anschluss dauerhaft zu deaktivieren, entfernen Sie die OCPP-Konfiguration in Ihrer Wallbox."));

        Register(x => x.AutoSwitchBetween1And3PhasesEnabled,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Auto switch between 1 and 3 phases",
                "When enabled the charger can automatically switch between a 1 and 3 phase charge. Note: Most of the chargers do not support this and some cars might get a hardware damage if enabled, so enable with care."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Automatisch zwischen 1 und 3 Phasen wechseln",
                "Wenn aktiviert, kann die Wallbox automatisch zwischen ein- und dreiphasigem Laden wechseln. Hinweis: Die meisten Ladegeräte unterstützen das nicht und manche Fahrzeuge könnten Schaden nehmen – daher nur mit Vorsicht aktivieren."));

        Register(x => x.PhaseSwitchCoolDownTimeSeconds,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                null,
                "Some chargers or cars need additional time after switch off to start charging with a different number of phases. To delay a charge start after a phase switch set a value in seconds here."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                null,
                "Einige Ladegeräte oder Fahrzeuge benötigen nach dem Abschalten zusätzliche Zeit, um mit einer anderen Phasenanzahl zu laden. Verzögern Sie den Ladebeginn nach einem Phasenwechsel, indem Sie hier einen Wert in Sekunden eintragen."));

        Register(x => x.MinCurrent,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                null,
                "The minimum current that the charging point is allowed to use. Charging will never be slower than this current. Note: This value does not have any influence on when charging stops completly, you will find more details on \"Switch Off Current\". Recommended Value: 6."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                null,
                "Der minimale Strom, den der Ladepunkt nutzen darf. Das Laden wird niemals langsamer als dieser Wert. Hinweis: Dieser Wert beeinflusst nicht, wann der Ladevorgang vollständig stoppt – mehr dazu unter ‚Abschaltstrom‘. Empfohlener Wert: 6."));

        Register(x => x.SwitchOffAtCurrent,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                null,
                "The charging point will stop charging when the available current drops below this value. This allows charging to continue for a while even if the current dips slightly, preventing unnecessary interruptions. Note: If you set this value to e.g. 3A while Min Current is set to 6A, charging will continue with 6A as long as there is enough solar power for 3A. Recommended Value: 6."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                null,
                "Der Ladepunkt beendet den Ladevorgang, wenn der verfügbare Strom unter diesen Wert fällt. So kann das Laden auch bei kurzfristigen Einbrüchen fortgesetzt werden und unnötige Unterbrechungen werden vermieden. Hinweis: Wenn Sie diesen Wert z. B. auf 3 A setzen, während der Mindeststrom 6 A beträgt, lädt das Fahrzeug weiter mit 6 A, solange ausreichend Solarstrom für 3 A vorhanden ist. Empfohlener Wert: 6."));

        Register(x => x.SwitchOnAtCurrent,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                null,
                "The charging point will only begin charging when the available current exceeds this value. Helps to avoid starting the charging process too frequently due to small current fluctuations. Recommended Value: 8."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                null,
                "Der Ladepunkt beginnt erst mit dem Laden, wenn der verfügbare Strom diesen Wert überschreitet. Das verhindert häufige Startvorgänge durch kleine Stromschwankungen. Empfohlener Wert: 8."));

        Register(x => x.MaxCurrent,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                null,
                "The maximum current that the charging point is allowed to use. Charging will be limited to this value even if more current is available. Recommended Value: The maximum current permitted by the circuit breaker, wiring, and wallbox. If you're unsure about this value, please contact a qualified electrician."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                null,
                "Der maximale Strom, den der Ladepunkt verwenden darf. Der Ladevorgang wird auf diesen Wert begrenzt, selbst wenn mehr Strom verfügbar ist. Empfohlener Wert: Der maximal zulässige Strom von Sicherung, Verkabelung und Wallbox. Bei Unsicherheit bitte einen qualifizierten Elektriker kontaktieren."));

        Register(x => x.ConnectedPhasesCount,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                null,
                "Number of connected phases on your charging station. If you're unsure about this value, please contact a qualified electrician. Note: Do not enter the number of phases the car can handle, just the number of phases the charging connector is connected to!"),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                null,
                "Anzahl der an Ihrer Wallbox angeschlossenen Phasen. Wenn Sie unsicher sind, fragen Sie bitte einen qualifizierten Elektriker. Hinweis: Tragen Sie nicht die Anzahl der Phasen ein, die das Fahrzeug verarbeiten kann, sondern nur die Phasen, mit denen der Ladeanschluss verbunden ist!"));

        Register(x => x.ChargingPriority,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                null,
                "If there is not enough power for all cars/charging connectors, the cars/charging connectors will be charged ordered by priority. Cars/Charging connectors with the same priority are ordered randomly."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                null,
                "Wenn nicht genug Leistung für alle Fahrzeuge/Ladepunkte vorhanden ist, werden sie entsprechend ihrer Priorität geladen. Fahrzeuge/Ladepunkte mit derselben Priorität werden zufällig angeordnet."));

        Register(x => x.AllowedCars,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                null,
                "Select all cars that may be connected to this connector. Is used to improve automatic detection of connected cars."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                null,
                "Wählen Sie alle Fahrzeuge aus, die an diesem Anschluss geladen werden dürfen. Dies verbessert die automatische Erkennung angeschlossener Fahrzeuge."));

        Register(x => x.AllowGuestCars,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                null,
                "Cars that are not known by TSC can charge here."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                null,
                "Hier können auch Fahrzeuge laden, die TSC nicht bekannt sind."));
    }
}
