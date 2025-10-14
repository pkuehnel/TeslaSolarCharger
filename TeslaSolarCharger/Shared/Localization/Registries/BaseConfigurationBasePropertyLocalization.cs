using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries;

public class BaseConfigurationBasePropertyLocalization : PropertyLocalizationRegistry<BaseConfigurationBase>
{
    protected override void Configure()
    {
        Register(x => x.HomeBatteryPowerInversionUrl,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "HomeBatteryPowerInversion Url",
                "Use this if you have to dynamically invert the home battery power. Note: Only 0 and 1 are allowed as response. As far as I know this is only needed with Sungrow Inverters."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "HomeBatteryPowerInversion-URL",
                "Verwenden Sie diese Option, wenn Sie die Leistung der Heimbatterie dynamisch invertieren müssen. Hinweis: Als Antwort sind nur 0 und 1 erlaubt. Nach aktuellem Stand ist dies nur bei Sungrow-Wechselrichtern erforderlich."));

        Register(x => x.UpdateIntervalSeconds,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Power Change Interval",
                "Every x seconds it is checked if any power changes are required."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Intervall für Leistungsänderungen",
                "Alle x Sekunden wird geprüft, ob Leistungsänderungen erforderlich sind."));

        Register(x => x.SkipPowerChangesOnLastAdjustmentNewerThanSeconds,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Skip Power Changes On Last Adjustment Newer Than Seconds",
                "Be cautious when setting values below 25 seconds as this might result in unexpected bahaviour as cars or charging stations might take some time to update the power."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Leistungsänderungen überspringen, wenn die letzte Anpassung neuer als Sekunden ist",
                "Seien Sie vorsichtig bei Werten unter 25 Sekunden, da Fahrzeuge oder Ladestationen möglicherweise Zeit benötigen, um die Leistung anzupassen."));

        Register(x => x.PvValueUpdateIntervalSeconds,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Solar power refresh interval",
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Aktualisierungsintervall für Solarleistung",
                null));

        Register(x => x.MinutesUntilSwitchOn,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Time with enough solar power until charging starts",
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Zeit mit ausreichender Solarleistung bis zum Ladebeginn",
                null));

        Register(x => x.MinutesUntilSwitchOff,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Time without enough solar power until charging stops",
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Zeit ohne ausreichende Solarleistung bis zum Ladeende",
                null));

        Register(x => x.PowerBuffer,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Power Buffer",
                "Set values higher than 0 to always have some overage (power to grid). Set values lower than 0 to always consume some power from the grid."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Leistungspuffer",
                "Werte über 0 sorgen für eine Einspeise-Reserve. Werte unter 0 führen dazu, dass immer etwas Netzleistung bezogen wird."));

        Register(x => x.AllowPowerBufferChangeOnHome,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Allow Power Buffer Change On Home",
                "If enabled, the configured power buffer is displayed on the home screen, including the option to directly change it."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Änderung des Leistungspuffers auf der Startseite erlauben",
                "Wenn aktiviert, wird der konfigurierte Leistungspuffer auf der Startseite angezeigt und kann dort direkt angepasst werden."));

        Register(x => x.PredictSolarPowerGeneration,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Predict Solar Power Generation",
                "If enabled, your home geofence location is transfered to the Solar4Car.com servers as well as to the servers of www.visualcrossing.com. At no point will your location data be linked with other data."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Solarenergieerzeugung vorhersagen",
                "Wenn aktiviert, wird Ihr Home-Geofence an die Server von Solar4Car.com sowie an www.visualcrossing.com übertragen. Ihre Positionsdaten werden dabei nie mit anderen Daten verknüpft."));

        Register(x => x.UsePredictedSolarPowerGenerationForChargingSchedules,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Use Predicted Solar Power Generation For Charging Schedules",
                "If enabled, when a target Soc is set not only grid prices but also estimated solar power generation is used to schedule charging."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Vorhergesagte Solarleistung für Ladepläne verwenden",
                "Wenn aktiviert und ein Ziel-Ladestand gesetzt ist, werden für die Ladeplanung neben den Netzpreisen auch die prognostizierte Solarleistung berücksichtigt."));

        Register(x => x.ShowEnergyDataOnHome,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Show Energy Data On Home",
                "This is in an early beta and might not behave like expected. Loading might take longer than 30 seconds or never load on low performance devices like Raspery Pi 3. This will be fixed in a future update."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Energiedaten auf der Startseite anzeigen",
                "Diese Funktion befindet sich in einer frühen Beta-Phase und verhält sich eventuell nicht wie erwartet. Auf Geräten mit geringer Leistung (z. B. Raspberry Pi 3) kann das Laden länger als 30 Sekunden dauern oder fehlschlagen. Dies wird in einem zukünftigen Update behoben."));

        Register(x => x.TelegramBotKey,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Telegram Bot Key",
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Telegram-Bot-Schlüssel",
                null));

        Register(x => x.TelegramChannelId,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Telegram Channel Id",
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Telegram-Kanal-ID",
                null));

        Register(x => x.SendStackTraceToTelegram,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Send Stack Trace To Telegram",
                "If enabled detailed error information are sent via Telegram so developers can find the root cause. This is not needed for normal usage."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Stacktrace an Telegram senden",
                "Wenn aktiviert, werden detaillierte Fehlerinformationen per Telegram versendet, damit Entwickler die Ursache finden können. Für den normalen Betrieb ist das nicht erforderlich."));

        Register(x => x.TeslaMateDbServer,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "TeslaMate Database Host",
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "TeslaMate-Datenbank-Host",
                null));

        Register(x => x.TeslaMateDbPort,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "TeslaMate Database Server Port",
                "You can use the internal port of the TeslaMate database container"),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "TeslaMate-Datenbankserver-Port",
                "Sie können den internen Port des TeslaMate-Datenbankcontainers verwenden."));

        Register(x => x.TeslaMateDbDatabaseName,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "TeslaMate Database Name",
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "TeslaMate-Datenbankname",
                null));

        Register(x => x.TeslaMateDbUser,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "TeslaMate Database Username",
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "TeslaMate-Datenbank-Benutzername",
                null));

        Register(x => x.TeslaMateDbPassword,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "TeslaMate Database Server Password",
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "TeslaMate-Datenbank-Passwort",
                null));

        Register(x => x.MosquitoServer,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Mosquito servername",
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Mosquitto-Servername",
                null));

        Register(x => x.MqqtClientId,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Mqqt ClientId",
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "MQTT-Client-ID",
                null));

        Register(x => x.DynamicHomeBatteryMinSoc,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Dynamic Home Battery Min Soc",
                "If enabled the Home Battery Min Soc is automatically set based on solar predictions to make sure the home battery is fully charged at the end of the day. This setting is only recommended after having solar predictions enabled for at least two weeks."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Dynamischer Mindest-Ladestand der Heimbatterie",
                "Wenn aktiviert, wird der minimale Ladestand der Heimbatterie automatisch anhand der Solarprognosen festgelegt, damit sie zum Tagesende voll ist. Diese Einstellung wird erst empfohlen, wenn die Solarprognosen mindestens zwei Wochen aktiv waren."));

        Register(x => x.HomeBatteryMinSoc,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Home Battery Minimum SoC",
                "Set the SoC your home battery should get charged to before cars start to use full power. Leave empty if you do not have a home battery"),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Minimaler Ladestand der Heimbatterie",
                "Legen Sie fest, bis zu welchem Ladestand die Heimbatterie geladen wird, bevor Fahrzeuge mit voller Leistung laden. Leer lassen, wenn keine Heimbatterie vorhanden ist."));

        Register(x => x.HomeBatteryMinDynamicMinSoc,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Home Battery Min Dynamic Min Soc",
                "Reserve that is always set as min SoC."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Minimaler dynamischer Mindest-Ladestand der Heimbatterie",
                "Reserve, die immer als minimaler Ladestand gesetzt wird."));

        Register(x => x.HomeBatteryMaxDynamicMinSoc,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Home Battery Max Dynamic Min Soc",
                "Min SoC is never set higher than this value."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Maximaler dynamischer Mindest-Ladestand der Heimbatterie",
                "Der minimale Ladestand wird nie höher als dieser Wert gesetzt."));

        Register(x => x.DynamicMinSocCalculationBuffer,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Dynamic Min Soc Calculation Buffer",
                "Used to make sure your home battery does not run out of power even if weather predictions are not correct or your house uses more energy than anticipated."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Berechnungspuffer für dynamischen Mindest-Ladestand",
                "Sorgt dafür, dass die Heimbatterie nicht leer wird, selbst wenn Wetterprognosen falsch liegen oder der Energiebedarf höher als erwartet ist."));

        Register(x => x.ForceFullHomeBatteryBySunset,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Force Full Home Battery By Sunset",
                "If enabled, the system charges the home battery so it is full by sunset. If disabled, the system only ensures the battery does not run empty before the next sunrise."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Heimbatterie bis Sonnenuntergang vollständig laden erzwingen",
                "Wenn aktiviert, wird die Heimbatterie bis zum Sonnenuntergang vollständig geladen. Wenn deaktiviert, wird lediglich sichergestellt, dass die Batterie vor dem nächsten Sonnenaufgang nicht leerläuft."));

        Register(x => x.HomeBatteryChargingPower,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Home Battery Target charging power",
                "Set the power your home battery should charge with as long as SoC is below set minimum SoC. Leave empty if you do not have a home battery"),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Ziel-Ladeleistung der Heimbatterie",
                "Legen Sie die Leistung fest, mit der die Heimbatterie geladen wird, solange der Ladestand unter dem Mindestwert liegt. Leer lassen, wenn keine Heimbatterie vorhanden ist."));

        Register(x => x.HomeBatteryDischargingPower,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Home Battery target discharging power",
                "Used to discharge the home battery when option is set in either a charging target or directly at the car."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Ziel-Entladeleistung der Heimbatterie",
                "Wird verwendet, um die Heimbatterie zu entladen, wenn die Option entweder in einem Ladeziel oder direkt am Fahrzeug gesetzt ist."));

        Register(x => x.HomeBatteryUsableEnergy,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Home Battery Usable energy",
                "Set the usable energy your home battery has."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Nutzbare Energie der Heimbatterie",
                "Geben Sie die nutzbare Energiemenge Ihrer Heimbatterie an."));

        Register(x => x.DischargeHomeBatteryToMinSocDuringDay,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Discharge Home Battery To Min Soc During Day",
                "When enabled TSC discharges the home battery to its Min Soc after sunrise and before sunset. Note: Charging of cars is only started if minimum difference between actual home battery soc and min soc is at least 10%."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Heimbatterie tagsüber auf Mindest-Ladestand entladen",
                "Wenn aktiviert, entlädt TSC die Heimbatterie zwischen Sonnenaufgang und Sonnenuntergang bis zum Mindest-Ladestand. Hinweis: Das Laden von Fahrzeugen startet erst, wenn die Differenz zwischen aktuellem und minimalem Ladestand mindestens 10 % beträgt."));

        Register(x => x.CarChargeLoss,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Car Charge Loss",
                "Energy lost when charging cars. Is used to calculate charging schedules based on battery capacity."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Ladeverlust des Fahrzeugs",
                "Energieverlust beim Laden der Fahrzeuge. Wird verwendet, um Ladepläne basierend auf der Batteriekapazität zu berechnen."));

        Register(x => x.MaxCombinedCurrent,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Max combined current",
                "Set a value if you want to reduce the max combined used current per phase of all cars. E.g. if you have two cars each set to max 16A but your installation can only handle 20A per phase you can set 20A here. So if one car uses 16A per phase the other car can only use 4A per phase. Note: Power is distributed based on the set car priorities."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Maximaler Gesamtstrom",
                "Legen Sie einen Wert fest, um den maximalen Gesamtstrom pro Phase über alle Fahrzeuge zu begrenzen. Beispiel: Zwei Fahrzeuge sind jeweils auf 16 A eingestellt, die Installation erlaubt jedoch nur 20 A pro Phase – stellen Sie hier 20 A ein. Nutzt ein Fahrzeug 16 A, stehen dem anderen nur noch 4 A zur Verfügung. Hinweis: Die Leistung wird entsprechend der eingestellten Fahrzeugprioritäten verteilt."));

        Register(x => x.MaxInverterAcPower,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Max Inverter AC Power",
                "If you have a hybrid inverter that has more DC than AC power insert the maximum AC Power here. This is a very rare, so in most cases you can leave this field empty."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Maximale AC-Leistung des Wechselrichters",
                "Wenn Ihr Hybridwechselrichter mehr DC- als AC-Leistung besitzt, tragen Sie hier die maximale AC-Leistung ein. Dies ist selten erforderlich und kann in den meisten Fällen leer bleiben."));

        Register(x => x.UseTeslaMateIntegration,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Use TeslaMate Integration",
                "When you use TeslaMate you can enable this so calculated charging costs from TSC are set in TeslaMate. Note: The charging costs in TeslaMate are only updated ever 24 hours."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "TeslaMate-Integration verwenden",
                "Wenn Sie TeslaMate nutzen, können Sie hier aktivieren, dass die von TSC berechneten Ladekosten in TeslaMate übernommen werden. Hinweis: Die Kosten werden in TeslaMate nur alle 24 Stunden aktualisiert."));

        Register(x => x.UseTeslaMateAsDataSource,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Use TeslaMate as Data Source",
                "If enabled TeslaMate MQTT is used as datasource. If disabled Tesla API is directly called. Note: If you use TSC without TeslaMate the setting here does not matter. Then the Tesla API is used always."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "TeslaMate als Datenquelle nutzen",
                "Wenn aktiviert, wird TeslaMate MQTT als Datenquelle verwendet. Wenn deaktiviert, ruft TSC direkt die Tesla-API auf. Hinweis: Wird TSC ohne TeslaMate betrieben, hat diese Einstellung keine Wirkung – es wird immer die Tesla-API genutzt."));

        Register(x => x.HomeGeofenceRadius,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Home Radius",
                "Increase or decrease the radius of the home geofence. Note: Values below 50m are note recommended"),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Radius Zuhause",
                "Erhöhen oder verringern Sie den Radius des Home-Geofences. Hinweis: Werte unter 50 m werden nicht empfohlen."));
    }
}
