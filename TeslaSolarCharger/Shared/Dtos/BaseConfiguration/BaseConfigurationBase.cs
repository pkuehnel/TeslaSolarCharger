using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using TeslaSolarCharger.Shared.Attributes;
using TeslaSolarCharger.Shared.Resources.BaseConfiguration;

namespace TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

public class BaseConfigurationBase
{
    public Version Version { get; set; } = new(1, 0);
    public string? SolarMqttServer { get; set; }
    public string? SolarMqttUserName { get; set; }
    [DataType(DataType.Password)]
    public string? SolarMqttPassword { get; set; }
    public string? CurrentPowerToGridMqttTopic { get; set; }
    public string? CurrentPowerToGridUrl { get; set; }
    public Dictionary<string, string> CurrentPowerToGridHeaders { get; set; } = new();
    public string? HomeBatterySocMqttTopic { get; set; }
    public string? HomeBatterySocUrl { get; set; }
    public Dictionary<string, string> HomeBatterySocHeaders { get; set; } = new();
    public string? HomeBatteryPowerMqttTopic { get; set; }
    public string? HomeBatteryPowerUrl { get; set; }
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.HomeBatteryPowerInversionUrl_DisplayName))]
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.HomeBatteryPowerInversionUrl_HelperText))]
    public string? HomeBatteryPowerInversionUrl { get; set; }
    public Dictionary<string, string> HomeBatteryPowerHeaders { get; set; } = new();
    public Dictionary<string, string> HomeBatteryPowerInversionHeaders { get; set; } = new();
    public bool IsModbusGridUrl { get; set; }
    public bool IsModbusHomeBatterySocUrl { get; set; }
    public bool IsModbusHomeBatteryPowerUrl { get; set; }
    public string? CurrentInverterPowerMqttTopic { get; set; }
    public string? CurrentInverterPowerUrl { get; set; }
    public Dictionary<string, string> CurrentInverterPowerHeaders { get; set; } = new();
    public bool IsModbusCurrentInverterPowerUrl { get; set; }
    [Required]
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.UpdateIntervalSeconds_DisplayName))]
    [Postfix("s")]
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.UpdateIntervalSeconds_HelperText))]
    public int UpdateIntervalSeconds { get; set; } = 30;
    [Required]
    [Postfix("s")]
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.SkipPowerChangesOnLastAdjustmentNewerThanSeconds_HelperText))]
    public int SkipPowerChangesOnLastAdjustmentNewerThanSeconds { get; set; } = 25;
    [Required]
    [Range(1, int.MaxValue)]
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.PvValueUpdateIntervalSeconds_DisplayName))]
    [Postfix("s")]
    public int? PvValueUpdateIntervalSeconds { get; set; } = 1;
    [Required]
    [Range(1, int.MaxValue)]
    [Postfix("s")]
    public int MaxModbusErrorBackoffDuration { get; set; } = 18000;
    [Required] 
    public string GeoFence { get; set; } = "Home";
    [Required]
    [Range(1, int.MaxValue)]
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.MinutesUntilSwitchOn_DisplayName))]
    [Postfix("min")]
    public int MinutesUntilSwitchOn { get; set; } = 5;
    [Required]
    [Range(1, int.MaxValue)]
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.MinutesUntilSwitchOff_DisplayName))]
    [Postfix("min")]
    public int MinutesUntilSwitchOff { get; set; } = 5;
    [Required]
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.PowerBuffer_DisplayName))]
    [Postfix("W")]
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.PowerBuffer_HelperText))]
    public int PowerBuffer { get; set; } = 0;
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.AllowPowerBufferChangeOnHome_HelperText))]
    public bool AllowPowerBufferChangeOnHome { get; set; }
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.PredictSolarPowerGeneration_HelperText))]
    public bool PredictSolarPowerGeneration { get; set; }
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.UsePredictedSolarPowerGenerationForChargingSchedules_HelperText))]
    public bool UsePredictedSolarPowerGenerationForChargingSchedules { get; set; }
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.ShowEnergyDataOnHome_HelperText))]
    public bool ShowEnergyDataOnHome { get; set; }
    public string? CurrentPowerToGridJsonPattern { get; set; }
    public decimal CurrentPowerToGridCorrectionFactor { get; set; } = 1;
    public string? CurrentInverterPowerJsonPattern { get; set; }
    public decimal CurrentInverterPowerCorrectionFactor { get; set; } = 1;
    public string? HomeBatterySocJsonPattern { get; set; }
    public decimal HomeBatterySocCorrectionFactor { get; set; } = 1;
    public string? HomeBatteryPowerJsonPattern { get; set; }
    public decimal HomeBatteryPowerCorrectionFactor { get; set; } = 1;
    [DisplayName("Telegram Bot Key")]
    public string? TelegramBotKey { get; set; }
    [DisplayName("Telegram Channel Id")]
    public string? TelegramChannelId { get; set; }
    [HelperText("If enabled detailed error information are sent via Telegram so developers can find the root cause. This is not needed for normal usage.")]
    public bool SendStackTraceToTelegram { get; set; }
    [DisplayName("TeslaMate Database Host")]
    public string? TeslaMateDbServer { get; set; }
    [DisplayName("TeslaMate Database Server Port")]
    [HelperText("You can use the internal port of the TeslaMate database container")]
    public int? TeslaMateDbPort { get; set; }
    [DisplayName("TeslaMate Database Name")]
    public string? TeslaMateDbDatabaseName { get; set; }
    [DisplayName("TeslaMate Database Username")]
    public string? TeslaMateDbUser { get; set; }
    [DataType(DataType.Password)]
    [DisplayName("TeslaMate Database Server Password")]
    public string? TeslaMateDbPassword { get; set; }
    [DisplayName("Mosquito servername")]
    public string? MosquitoServer { get; set; }
    [Required]
    [DisplayName("Mqqt ClientId")]
    public string MqqtClientId { get; set; } = "TeslaSolarCharger";
    public string? CurrentPowerToGridXmlPattern { get; set; }
    public string? CurrentPowerToGridXmlAttributeHeaderName { get; set; }
    public string? CurrentPowerToGridXmlAttributeHeaderValue { get; set; }
    public string? CurrentPowerToGridXmlAttributeValueName { get; set; }
    public string? CurrentInverterPowerXmlPattern { get; set; }
    public string? CurrentInverterPowerXmlAttributeHeaderName { get; set; }
    public string? CurrentInverterPowerXmlAttributeHeaderValue { get; set; }
    public string? CurrentInverterPowerXmlAttributeValueName { get; set; }
    public string? HomeBatterySocXmlPattern { get; set; }
    public string? HomeBatterySocXmlAttributeHeaderName { get; set; }
    public string? HomeBatterySocXmlAttributeHeaderValue { get; set; }
    public string? HomeBatterySocXmlAttributeValueName { get; set; }
    public string? HomeBatteryPowerXmlPattern { get; set; }
    public string? HomeBatteryPowerXmlAttributeHeaderName { get; set; }
    public string? HomeBatteryPowerXmlAttributeHeaderValue { get; set; }
    public string? HomeBatteryPowerXmlAttributeValueName { get; set; }

    [DisplayName("Dynamic Home Battery Min Soc")]
    [Postfix("%")]
    [HelperText("If enabled the Home Battery Min Soc is automatically set based on solar predictions to make sure the home battery is fully charged at the end of the day. This setting is only recommended after having solar predictions enabled for at least two weeks.")]
    public bool? DynamicHomeBatteryMinSoc { get; set; }
    [DisplayName("Home Battery Minimum SoC")]
    [Postfix("%")]
    [HelperText("Set the SoC your home battery should get charged to before cars start to use full power. Leave empty if you do not have a home battery")]
    public int? HomeBatteryMinSoc { get; set; }
    [Postfix("%")]
    [HelperText("Reserve that is always set as min SoC.")]
    public int HomeBatteryMinDynamicMinSoc { get; set; } = 5;
    [Postfix("%")]
    [HelperText("Min SoC is never set higher than this value.")]
    public int HomeBatteryMaxDynamicMinSoc { get; set; } = 95;
    [Postfix("%")]
    [HelperText("Used to make sure your home battery does not run out of power even if weather predictions are not correct or your house uses more energy than anticipated.")]
    public int DynamicMinSocCalculationBuffer { get; set; } = 50;
    [HelperText("If enabled, the system charges the home battery so it is full by sunset. If disabled, the system only ensures the battery does not run empty before the next sunrise.")]
    public bool ForceFullHomeBatteryBySunset { get; set; } = true;
    [DisplayName("Home Battery Target charging power")]
    [Postfix("W")]
    [HelperText(
        "Set the power your home battery should charge with as long as SoC is below set minimum SoC. Leave empty if you do not have a home battery")]
    public int? HomeBatteryChargingPower { get; set; }
    [DisplayName("Home Battery target discharging power")]
    [Postfix("W")]
    [HelperText(
        "Used to discharge the home battery when option is set in either a charging target or directly at the car.")]
    public int? HomeBatteryDischargingPower { get; set; }
    [DisplayName("Home Battery Usable energy")]
    [Postfix("kWh")]
    [HelperText("Set the usable energy your home battery has.")]
    public double? HomeBatteryUsableEnergy { get; set; }
    [HelperText("When enabled TSC discharges the home battery to its Min Soc after sunrise and before sunset. Note: Charging of cars is only started if minimum difference between actual home battery soc and min soc is at least 10%.")]
    public bool DischargeHomeBatteryToMinSocDuringDay { get; set; }
    [Postfix("%")]
    [HelperText("Energy lost when charging cars. Is used to calculate charging schedules based on battery capacity.")]
    public int CarChargeLoss { get; set; } = 15;
    [DisplayName("Max combined current")]
    [Postfix("A")]
    [HelperText("Set a value if you want to reduce the max combined used current per phase of all cars. E.g. if you have two cars each set to max 16A but your installation can only handle 20A per phase you can set 20A here. So if one car uses 16A per phase the other car can only use 4A per phase. Note: Power is distributed based on the set car priorities.")]
    public int? MaxCombinedCurrent { get; set; }
    [DisplayName("Max Inverter AC Power")]
    [Postfix("W")]
    [HelperText("If you have a hybrid inverter that has more DC than AC power insert the maximum AC Power here. This is a very rare, so in most cases you can leave this field empty.")]
    public int? MaxInverterAcPower { get; set; }
    public string? BleApiBaseUrl { get; set; }
    [DisplayName("Use TeslaMate Integration")]
    [HelperText("When you use TeslaMate you can enable this so calculated charging costs from TSC are set in TeslaMate. Note: The charging costs in TeslaMate are only updated ever 24 hours.")]
    public bool UseTeslaMateIntegration { get; set; }
    [DisplayName("Use TeslaMate as Data Source")]
    [HelperText("If enabled TeslaMate MQTT is used as datasource. If disabled Tesla API is directly called. Note: If you use TSC without TeslaMate the setting here does not matter. Then the Tesla API is used always.")]
    public bool UseTeslaMateAsDataSource { get; set; }
    public double HomeGeofenceLongitude { get; set; } = 13.3761736; //Do not change the default value as depending on this the Geofence from TeslaMate is converted or not
    public double HomeGeofenceLatitude { get; set; } = 52.5185238; //Do not change the default value as depending on this the Geofence from TeslaMate is converted or not
    [DisplayName("Home Radius")]
    [Postfix("m")]
    [HelperText("Increase or decrease the radius of the home geofence. Note: Values below 50m are note recommended")]
    public int HomeGeofenceRadius { get; set; } = 50;

    public FrontendConfiguration? FrontendConfiguration { get; set; }

}
