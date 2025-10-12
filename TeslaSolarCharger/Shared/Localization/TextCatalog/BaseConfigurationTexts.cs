using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.TextCatalog;

public static class BaseConfigurationTexts
{
    public static LocalizedText SolarMqttServer { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.SolarMqttServer, "Solar MQTT Server", "Solar-MQTT-Server");

    public static LocalizedText SolarMqttUserName { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.SolarMqttUserName, "Solar MQTT user name", "Solar-MQTT-Benutzername");

    public static LocalizedText SolarMqttPassword { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.SolarMqttPassword, "Solar MQTT password", "Solar-MQTT-Passwort");

    public static LocalizedText CurrentPowerToGridMqttTopic { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.CurrentPowerToGridMqttTopic, "Grid export MQTT topic", "MQTT-Thema für Netzeinspeisung");

    public static LocalizedText CurrentPowerToGridUrl { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.CurrentPowerToGridUrl, "Grid export URL", "URL für Netzeinspeisung");

    public static LocalizedText CurrentPowerToGridHeaders { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.CurrentPowerToGridHeaders, "Grid export headers", "Header für Netzeinspeisung");

    public static LocalizedText HomeBatterySocMqttTopic { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatterySocMqttTopic, "Home battery SoC MQTT topic", "MQTT-Thema für Heimbatterie-SoC");

    public static LocalizedText HomeBatterySocUrl { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatterySocUrl, "Home battery SoC URL", "URL für Heimbatterie-SoC");

    public static LocalizedText HomeBatterySocHeaders { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatterySocHeaders, "Home battery SoC headers", "Header für Heimbatterie-SoC");

    public static LocalizedText HomeBatteryPowerMqttTopic { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatteryPowerMqttTopic, "Home battery power MQTT topic", "MQTT-Thema für Heimbatterieleistung");

    public static LocalizedText HomeBatteryPowerUrl { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatteryPowerUrl, "Home battery power URL", "URL für Heimbatterieleistung");

    public static LocalizedText HomeBatteryPowerInversionUrl { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatteryPowerInversionUrl, "Home battery power inversion URL", "URL zur Invertierung der Heimbatterieleistung");

    public static LocalizedText HomeBatteryPowerInversionUrlHelper { get; } =
        LocalizedTextFactory.Create("Use this if you have to dynamically invert the home battery power. Note: Only 0 and 1 are allowed as response. As far as I know this is only needed with Sungrow Inverters.",
            "Verwende diese URL, wenn die Heimbatterieleistung dynamisch invertiert werden muss. Hinweis: Als Antwort sind nur 0 und 1 erlaubt. Nach aktuellem Wissen ist dies nur bei Sungrow-Wechselrichtern erforderlich.");

    public static LocalizedText HomeBatteryPowerHeaders { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatteryPowerHeaders, "Home battery power headers", "Header für Heimbatterieleistung");

    public static LocalizedText HomeBatteryPowerInversionHeaders { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatteryPowerInversionHeaders, "Home battery power inversion headers", "Header für invertierte Heimbatterieleistung");

    public static LocalizedText IsModbusGridUrl { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.IsModbusGridUrl, "Grid data via Modbus", "Netzdaten über Modbus");

    public static LocalizedText IsModbusHomeBatterySocUrl { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.IsModbusHomeBatterySocUrl, "Home battery SoC via Modbus", "Heimbatterie-SoC über Modbus");

    public static LocalizedText IsModbusHomeBatteryPowerUrl { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.IsModbusHomeBatteryPowerUrl, "Home battery power via Modbus", "Heimbatterieleistung über Modbus");

    public static LocalizedText CurrentInverterPowerMqttTopic { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.CurrentInverterPowerMqttTopic, "Inverter power MQTT topic", "MQTT-Thema für Wechselrichterleistung");

    public static LocalizedText CurrentInverterPowerUrl { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.CurrentInverterPowerUrl, "Inverter power URL", "URL für Wechselrichterleistung");

    public static LocalizedText CurrentInverterPowerHeaders { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.CurrentInverterPowerHeaders, "Inverter power headers", "Header für Wechselrichterleistung");

    public static LocalizedText IsModbusCurrentInverterPowerUrl { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.IsModbusCurrentInverterPowerUrl, "Inverter power via Modbus", "Wechselrichterleistung über Modbus");

    public static LocalizedText UpdateIntervalSeconds { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.UpdateIntervalSeconds, "Power change interval", "Intervall für Leistungsänderungen");

    public static LocalizedText UpdateIntervalSecondsHelper { get; } =
        LocalizedTextFactory.Create("Every x seconds it is checked if any power changes are required.",
            "In diesem Intervall wird geprüft, ob Leistungsanpassungen erforderlich sind.");

    public static LocalizedText SkipPowerChangesOnLastAdjustmentNewerThanSeconds { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.SkipPowerChangesOnLastAdjustmentNewerThanSeconds, "Ignore changes if the last adjustment is newer than", "Änderungen ignorieren, wenn die letzte Anpassung neuer ist als");

    public static LocalizedText SkipPowerChangesOnLastAdjustmentNewerThanSecondsHelper { get; } =
        LocalizedTextFactory.Create("Be cautious when setting values below 25 seconds as this might result in unexpected bahaviour as cars or charging stations might take some time to update the power.",
            "Werte unter 25 Sekunden können zu unerwartetem Verhalten führen, da Fahrzeuge oder Ladestationen etwas Zeit für die Aktualisierung der Leistung benötigen.");

    public static LocalizedText PvValueUpdateIntervalSeconds { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.PvValueUpdateIntervalSeconds, "Solar power refresh interval", "Aktualisierungsintervall für Solarleistung");

    public static LocalizedText MaxModbusErrorBackoffDuration { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.MaxModbusErrorBackoffDuration, "Maximum Modbus error backoff", "Maximale Modbus-Fehlerrückfallzeit");

    public static LocalizedText GeoFence { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.GeoFence, "Home geofence", "Heim-Geofence");

    public static LocalizedText MinutesUntilSwitchOn { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.MinutesUntilSwitchOn, "Time with enough solar power until charging starts", "Dauer mit ausreichender Solarleistung bis der Ladevorgang startet");

    public static LocalizedText MinutesUntilSwitchOff { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.MinutesUntilSwitchOff, "Time without enough solar power until charging stops", "Dauer ohne ausreichende Solarleistung bis der Ladevorgang stoppt");

    public static LocalizedText PowerBuffer { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.PowerBuffer, "Power buffer", "Leistungspuffer");

    public static LocalizedText PowerBufferHelper { get; } =
        LocalizedTextFactory.Create("Set values higher than 0 to always have some overage (power to grid). Set values lower than 0 to always consume some power from the grid.",
            "Werte über 0 sorgen stets für eine Einspeisung ins Netz. Werte unter 0 führen dazu, dass immer etwas Netzleistung bezogen wird.");

    public static LocalizedText AllowPowerBufferChangeOnHome { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.AllowPowerBufferChangeOnHome, "Allow power buffer change on home screen", "Änderung des Leistungspuffers auf der Startseite erlauben");

    public static LocalizedText AllowPowerBufferChangeOnHomeHelper { get; } =
        LocalizedTextFactory.Create("If enabled, the configured power buffer is displayed on the home screen, including the option to directly change it.",
            "Ist diese Option aktiv, wird der eingestellte Leistungspuffer auf der Startseite angezeigt und kann dort direkt geändert werden.");

    public static LocalizedText PredictSolarPowerGeneration { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.PredictSolarPowerGeneration, "Share geofence for solar prediction", "Geofence für Solarprognose teilen");

    public static LocalizedText PredictSolarPowerGenerationHelper { get; } =
        LocalizedTextFactory.Create("If enabled, your home geofence location is transfered to the Solar4Car.com servers as well as to the servers of www.visualcrossing.com. At no point will your location data be linked with other data.",
            "Wenn aktiviert, werden die Koordinaten des Heim-Geofence an die Server von Solar4Car.com sowie www.visualcrossing.com übertragen. Die Standortdaten werden niemals mit anderen Daten verknüpft.");

    public static LocalizedText UsePredictedSolarPowerGenerationForChargingSchedules { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.UsePredictedSolarPowerGenerationForChargingSchedules, "Use solar prediction for charging schedules", "Solarprognose für Ladepläne verwenden");

    public static LocalizedText UsePredictedSolarPowerGenerationForChargingSchedulesHelper { get; } =
        LocalizedTextFactory.Create("If enabled, when a target Soc is set not only grid prices but also estimated solar power generation is used to schedule charging.",
            "Wenn aktiviert, werden bei gesetztem Ziel-SoC neben den Netzpreisen auch die prognostizierten Solarleistungen für die Ladeplanung berücksichtigt.");

    public static LocalizedText ShowEnergyDataOnHome { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.ShowEnergyDataOnHome, "Show energy data on home screen", "Energiedaten auf der Startseite anzeigen");

    public static LocalizedText ShowEnergyDataOnHomeHelper { get; } =
        LocalizedTextFactory.Create("This is in an early beta and might not behave like expected. Loading might take longer than 30 seconds or never load on low performance devices like Raspery Pi 3. This will be fixed in a future update.",
            "Diese Funktion befindet sich in einer frühen Beta-Phase und kann sich unerwartet verhalten. Das Laden kann länger als 30 Sekunden dauern oder auf leistungsschwachen Geräten wie dem Raspberry Pi 3 fehlschlagen. Dies wird in einem zukünftigen Update verbessert.");

    public static LocalizedText CurrentPowerToGridJsonPattern { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.CurrentPowerToGridJsonPattern, "Grid export JSON path", "JSON-Pfad für Netzeinspeisung");

    public static LocalizedText CurrentPowerToGridCorrectionFactor { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.CurrentPowerToGridCorrectionFactor, "Grid export correction factor", "Korrekturfaktor für Netzeinspeisung");

    public static LocalizedText CurrentInverterPowerJsonPattern { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.CurrentInverterPowerJsonPattern, "Inverter power JSON path", "JSON-Pfad für Wechselrichterleistung");

    public static LocalizedText CurrentInverterPowerCorrectionFactor { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.CurrentInverterPowerCorrectionFactor, "Inverter power correction factor", "Korrekturfaktor für Wechselrichterleistung");

    public static LocalizedText HomeBatterySocJsonPattern { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatterySocJsonPattern, "Home battery SoC JSON path", "JSON-Pfad für Heimbatterie-SoC");

    public static LocalizedText HomeBatterySocCorrectionFactor { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatterySocCorrectionFactor, "Home battery SoC correction factor", "Korrekturfaktor für Heimbatterie-SoC");

    public static LocalizedText HomeBatteryPowerJsonPattern { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatteryPowerJsonPattern, "Home battery power JSON path", "JSON-Pfad für Heimbatterieleistung");

    public static LocalizedText HomeBatteryPowerCorrectionFactor { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatteryPowerCorrectionFactor, "Home battery power correction factor", "Korrekturfaktor für Heimbatterieleistung");

    public static LocalizedText TelegramBotKey { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.TelegramBotKey, "Telegram bot key", "Telegram-Bot-Schlüssel");

    public static LocalizedText TelegramChannelId { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.TelegramChannelId, "Telegram channel ID", "Telegram-Kanal-ID");

    public static LocalizedText SendStackTraceToTelegram { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.SendStackTraceToTelegram, "Send stack traces to Telegram", "Stacktraces an Telegram senden");

    public static LocalizedText SendStackTraceToTelegramHelper { get; } =
        LocalizedTextFactory.Create("If enabled detailed error information are sent via Telegram so developers can find the root cause. This is not needed for normal usage.",
            "Wenn aktiviert, werden detaillierte Fehlermeldungen über Telegram gesendet, damit Entwickler die Ursache finden können. Für den normalen Betrieb ist dies nicht erforderlich.");

    public static LocalizedText TeslaMateDbServer { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.TeslaMateDbServer, "TeslaMate database host", "TeslaMate-Datenbankserver");

    public static LocalizedText TeslaMateDbPort { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.TeslaMateDbPort, "TeslaMate database server port", "Port des TeslaMate-Datenbankservers");

    public static LocalizedText TeslaMateDbPortHelper { get; } =
        LocalizedTextFactory.Create("You can use the internal port of the TeslaMate database container", "Du kannst den internen Port des TeslaMate-Datenbankcontainers verwenden.");

    public static LocalizedText TeslaMateDbDatabaseName { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.TeslaMateDbDatabaseName, "TeslaMate database name", "TeslaMate-Datenbankname");

    public static LocalizedText TeslaMateDbUser { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.TeslaMateDbUser, "TeslaMate database username", "TeslaMate-Datenbankbenutzer");

    public static LocalizedText TeslaMateDbPassword { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.TeslaMateDbPassword, "TeslaMate database password", "TeslaMate-Datenbankpasswort");

    public static LocalizedText MosquitoServer { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.MosquitoServer, "Mosquitto server name", "Mosquitto-Servername");

    public static LocalizedText MqqtClientId { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.MqqtClientId, "MQTT client ID", "MQTT-Client-ID");

    public static LocalizedText CurrentPowerToGridXmlPattern { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.CurrentPowerToGridXmlPattern, "Grid export XML path", "XML-Pfad für Netzeinspeisung");

    public static LocalizedText CurrentPowerToGridXmlAttributeHeaderName { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.CurrentPowerToGridXmlAttributeHeaderName, "Grid export XML header name", "XML-Headername für Netzeinspeisung");

    public static LocalizedText CurrentPowerToGridXmlAttributeHeaderValue { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.CurrentPowerToGridXmlAttributeHeaderValue, "Grid export XML header value", "XML-Headerwert für Netzeinspeisung");

    public static LocalizedText CurrentPowerToGridXmlAttributeValueName { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.CurrentPowerToGridXmlAttributeValueName, "Grid export XML value name", "XML-Wertename für Netzeinspeisung");

    public static LocalizedText CurrentInverterPowerXmlPattern { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.CurrentInverterPowerXmlPattern, "Inverter power XML path", "XML-Pfad für Wechselrichterleistung");

    public static LocalizedText CurrentInverterPowerXmlAttributeHeaderName { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.CurrentInverterPowerXmlAttributeHeaderName, "Inverter power XML header name", "XML-Headername für Wechselrichterleistung");

    public static LocalizedText CurrentInverterPowerXmlAttributeHeaderValue { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.CurrentInverterPowerXmlAttributeHeaderValue, "Inverter power XML header value", "XML-Headerwert für Wechselrichterleistung");

    public static LocalizedText CurrentInverterPowerXmlAttributeValueName { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.CurrentInverterPowerXmlAttributeValueName, "Inverter power XML value name", "XML-Wertename für Wechselrichterleistung");

    public static LocalizedText HomeBatterySocXmlPattern { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatterySocXmlPattern, "Home battery SoC XML path", "XML-Pfad für Heimbatterie-SoC");

    public static LocalizedText HomeBatterySocXmlAttributeHeaderName { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatterySocXmlAttributeHeaderName, "Home battery SoC XML header name", "XML-Headername für Heimbatterie-SoC");

    public static LocalizedText HomeBatterySocXmlAttributeHeaderValue { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatterySocXmlAttributeHeaderValue, "Home battery SoC XML header value", "XML-Headerwert für Heimbatterie-SoC");

    public static LocalizedText HomeBatterySocXmlAttributeValueName { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatterySocXmlAttributeValueName, "Home battery SoC XML value name", "XML-Wertename für Heimbatterie-SoC");

    public static LocalizedText HomeBatteryPowerXmlPattern { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatteryPowerXmlPattern, "Home battery power XML path", "XML-Pfad für Heimbatterieleistung");

    public static LocalizedText HomeBatteryPowerXmlAttributeHeaderName { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatteryPowerXmlAttributeHeaderName, "Home battery power XML header name", "XML-Headername für Heimbatterieleistung");

    public static LocalizedText HomeBatteryPowerXmlAttributeHeaderValue { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatteryPowerXmlAttributeHeaderValue, "Home battery power XML header value", "XML-Headerwert für Heimbatterieleistung");

    public static LocalizedText HomeBatteryPowerXmlAttributeValueName { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatteryPowerXmlAttributeValueName, "Home battery power XML value name", "XML-Wertename für Heimbatterieleistung");

    public static LocalizedText DynamicHomeBatteryMinSoc { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.DynamicHomeBatteryMinSoc, "Dynamic home battery minimum SoC", "Dynamisches Mindest-SoC der Heimbatterie");

    public static LocalizedText DynamicHomeBatteryMinSocHelper { get; } =
        LocalizedTextFactory.Create("If enabled the Home Battery Min Soc is automatically set based on solar predictions to make sure the home battery is fully charged at the end of the day. This setting is only recommended after having solar predictions enabled for at least two weeks.",
            "Wenn aktiviert, wird das Mindest-SoC der Heimbatterie anhand der Solarprognosen automatisch so eingestellt, dass die Batterie zum Tagesende voll ist. Diese Einstellung wird erst nach mindestens zwei Wochen mit aktivierter Solarprognose empfohlen.");

    public static LocalizedText HomeBatteryMinSoc { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatteryMinSoc, "Home battery minimum SoC", "Mindest-SoC der Heimbatterie");

    public static LocalizedText HomeBatteryMinSocHelper { get; } =
        LocalizedTextFactory.Create("Set the SoC your home battery should get charged to before cars start to use full power. Leave empty if you do not have a home battery",
            "Lege fest, bis zu welchem SoC die Heimbatterie geladen werden soll, bevor Fahrzeuge die volle Leistung nutzen. Leer lassen, wenn keine Heimbatterie vorhanden ist.");

    public static LocalizedText HomeBatteryMinDynamicMinSoc { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatteryMinDynamicMinSoc, "Dynamic minimum SoC reserve", "Dynamische Mindest-SoC-Reserve");

    public static LocalizedText HomeBatteryMinDynamicMinSocHelper { get; } =
        LocalizedTextFactory.Create("Reserve that is always set as min SoC.", "Reserve, die stets als Mindest-SoC gesetzt wird.");

    public static LocalizedText HomeBatteryMaxDynamicMinSoc { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatteryMaxDynamicMinSoc, "Maximum dynamic minimum SoC", "Maximaler dynamischer Mindest-SoC");

    public static LocalizedText HomeBatteryMaxDynamicMinSocHelper { get; } =
        LocalizedTextFactory.Create("Min SoC is never set higher than this value.", "Das Mindest-SoC wird niemals höher als dieser Wert gesetzt.");

    public static LocalizedText DynamicMinSocCalculationBuffer { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.DynamicMinSocCalculationBuffer, "Dynamic SoC calculation buffer", "Puffer für dynamische SoC-Berechnung");

    public static LocalizedText DynamicMinSocCalculationBufferHelper { get; } =
        LocalizedTextFactory.Create("Used to make sure your home battery does not run out of power even if weather predictions are not correct or your house uses more energy than anticipated.",
            "Stellt sicher, dass die Heimbatterie nicht leer läuft, selbst wenn Wetterprognosen ungenau sind oder der Stromverbrauch höher als erwartet ist.");

    public static LocalizedText ForceFullHomeBatteryBySunset { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.ForceFullHomeBatteryBySunset, "Charge home battery to full by sunset", "Heimbatterie bis Sonnenuntergang vollständig laden");

    public static LocalizedText ForceFullHomeBatteryBySunsetHelper { get; } =
        LocalizedTextFactory.Create("If enabled, the system charges the home battery so it is full by sunset. If disabled, the system only ensures the battery does not run empty before the next sunrise.",
            "Wenn aktiviert, lädt das System die Heimbatterie bis zum Sonnenuntergang vollständig. Andernfalls wird nur sichergestellt, dass die Batterie vor dem nächsten Sonnenaufgang nicht leerläuft.");

    public static LocalizedText HomeBatteryChargingPower { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatteryChargingPower, "Home battery target charging power", "Ziel-Ladeleistung der Heimbatterie");

    public static LocalizedText HomeBatteryChargingPowerHelper { get; } =
        LocalizedTextFactory.Create("Set the power your home battery should charge with as long as SoC is below set minimum SoC. Leave empty if you do not have a home battery",
            "Lege fest, mit welcher Leistung die Heimbatterie geladen werden soll, solange das SoC unter dem Mindestwert liegt. Leer lassen, wenn keine Heimbatterie vorhanden ist.");

    public static LocalizedText HomeBatteryDischargingPower { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatteryDischargingPower, "Home battery target discharging power", "Ziel-Entladeleistung der Heimbatterie");

    public static LocalizedText HomeBatteryDischargingPowerHelper { get; } =
        LocalizedTextFactory.Create("Used to discharge the home battery when option is set in either a charging target or directly at the car.",
            "Wird verwendet, um die Heimbatterie zu entladen, wenn dies in einem Ladeziel oder direkt am Fahrzeug eingestellt ist.");

    public static LocalizedText HomeBatteryUsableEnergy { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeBatteryUsableEnergy, "Home battery usable energy", "Nutzbare Energie der Heimbatterie");

    public static LocalizedText HomeBatteryUsableEnergyHelper { get; } =
        LocalizedTextFactory.Create("Set the usable energy your home battery has.", "Lege die nutzbare Energie deiner Heimbatterie fest.");

    public static LocalizedText DischargeHomeBatteryToMinSocDuringDay { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.DischargeHomeBatteryToMinSocDuringDay, "Discharge home battery to minimum during the day", "Heimbatterie tagsüber auf Mindest-SoC entladen");

    public static LocalizedText DischargeHomeBatteryToMinSocDuringDayHelper { get; } =
        LocalizedTextFactory.Create("When enabled TSC discharges the home battery to its Min Soc after sunrise and before sunset. Note: Charging of cars is only started if minimum difference between actual home battery soc and min soc is at least 10%.",
            "Wenn aktiviert, entlädt TSC die Heimbatterie nach Sonnenaufgang und vor Sonnenuntergang auf das Mindest-SoC. Hinweis: Fahrzeuge werden nur geladen, wenn der Unterschied zwischen aktuellem und Mindest-SoC mindestens 10 % beträgt.");

    public static LocalizedText CarChargeLoss { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.CarChargeLoss, "Car charging losses", "Ladeverluste des Fahrzeugs");

    public static LocalizedText CarChargeLossHelper { get; } =
        LocalizedTextFactory.Create("Energy lost when charging cars. Is used to calculate charging schedules based on battery capacity.",
            "Energieverlust beim Laden von Fahrzeugen. Wird verwendet, um Ladepläne basierend auf der Batteriekapazität zu berechnen.");

    public static LocalizedText MaxCombinedCurrent { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.MaxCombinedCurrent, "Max combined current", "Maximaler Gesamtstrom");

    public static LocalizedText MaxCombinedCurrentHelper { get; } =
        LocalizedTextFactory.Create("Set a value if you want to reduce the max combined used current per phase of all cars. E.g. if you have two cars each set to max 16A but your installation can only handle 20A per phase you can set 20A here. So if one car uses 16A per phase the other car can only use 4A per phase. Note: Power is distributed based on the set car priorities.",
            "Lege einen Wert fest, um den maximalen Gesamtstrom pro Phase aller Fahrzeuge zu begrenzen. Beispiel: Haben zwei Fahrzeuge jeweils 16 A, deine Installation erlaubt aber nur 20 A pro Phase, kannst du hier 20 A setzen. Nutzt ein Fahrzeug 16 A, bleiben dem anderen 4 A. Hinweis: Die Leistung wird basierend auf den Fahrzeugprioritäten verteilt.");

    public static LocalizedText MaxInverterAcPower { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.MaxInverterAcPower, "Max inverter AC power", "Maximale Wechselrichter-AC-Leistung");

    public static LocalizedText MaxInverterAcPowerHelper { get; } =
        LocalizedTextFactory.Create("If you have a hybrid inverter that has more DC than AC power insert the maximum AC Power here. This is a very rare, so in most cases you can leave this field empty.",
            "Wenn du einen Hybrid-Wechselrichter mit höherer DC- als AC-Leistung hast, gib hier die maximale AC-Leistung an. Dies ist sehr selten, daher kann das Feld in den meisten Fällen leer bleiben.");

    public static LocalizedText BleApiBaseUrl { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.BleApiBaseUrl, "BLE API base URL", "Basis-URL der BLE-API");

    public static LocalizedText UseTeslaMateIntegration { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.UseTeslaMateIntegration, "Use TeslaMate integration", "TeslaMate-Integration verwenden");

    public static LocalizedText UseTeslaMateIntegrationHelper { get; } =
        LocalizedTextFactory.Create("When you use TeslaMate you can enable this so calculated charging costs from TSC are set in TeslaMate. Note: The charging costs in TeslaMate are only updated ever 24 hours.",
            "Wenn du TeslaMate verwendest, kannst du dies aktivieren, damit TSC die berechneten Ladekosten in TeslaMate überträgt. Hinweis: Die Kosten werden in TeslaMate nur alle 24 Stunden aktualisiert.");

    public static LocalizedText UseTeslaMateAsDataSource { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.UseTeslaMateAsDataSource, "Use TeslaMate as data source", "TeslaMate als Datenquelle verwenden");

    public static LocalizedText UseTeslaMateAsDataSourceHelper { get; } =
        LocalizedTextFactory.Create("If enabled TeslaMate MQTT is used as datasource. If disabled Tesla API is directly called. Note: If you use TSC without TeslaMate the setting here does not matter. Then the Tesla API is used always.",
            "Wenn aktiviert, wird TeslaMate MQTT als Datenquelle verwendet. Wenn deaktiviert, greift TSC direkt auf die Tesla-API zu. Ohne TeslaMate spielt diese Einstellung keine Rolle – es wird immer die Tesla-API genutzt.");

    public static LocalizedText HomeGeofenceLongitude { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeGeofenceLongitude, "Home geofence longitude", "Längengrad des Heim-Geofence");

    public static LocalizedText HomeGeofenceLatitude { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeGeofenceLatitude, "Home geofence latitude", "Breitengrad des Heim-Geofence");

    public static LocalizedText HomeGeofenceRadius { get; } =
        LocalizedTextFactory.CreateForProperty<BaseConfigurationBase>(b => b.HomeGeofenceRadius, "Home radius", "Heimradius");

    public static LocalizedText HomeGeofenceRadiusHelper { get; } =
        LocalizedTextFactory.Create("Increase or decrease the radius of the home geofence. Note: Values below 50m are note recommended",
            "Passe den Radius des Heim-Geofence an. Hinweis: Werte unter 50 m werden nicht empfohlen.");
}
