using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using TeslaSolarCharger.Shared.Attributes;
using TeslaSolarCharger.Shared.Localization;

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
    [DisplayName(LocalizationKeys.BaseConfiguration.HomeBatteryPowerInversionUrl_DisplayName)]
    [HelperText(LocalizationKeys.BaseConfiguration.HomeBatteryPowerInversionUrl_HelperText)]
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
    [DisplayName(LocalizationKeys.BaseConfiguration.UpdateIntervalSeconds_DisplayName)]
    [Postfix("s")]
    [HelperText(LocalizationKeys.BaseConfiguration.UpdateIntervalSeconds_HelperText)]
    public int UpdateIntervalSeconds { get; set; } = 30;
    [Required]
    [Postfix("s")]
    [HelperText(LocalizationKeys.BaseConfiguration.SkipPowerChangesOnLastAdjustmentNewerThanSeconds_HelperText)]
    public int SkipPowerChangesOnLastAdjustmentNewerThanSeconds { get; set; } = 25;
    [Required]
    [Range(1, int.MaxValue)]
    [DisplayName(LocalizationKeys.BaseConfiguration.PvValueUpdateIntervalSeconds_DisplayName)]
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
    [DisplayName(LocalizationKeys.BaseConfiguration.MinutesUntilSwitchOn_DisplayName)]
    [Postfix("min")]
    public int MinutesUntilSwitchOn { get; set; } = 5;
    [Required]
    [Range(1, int.MaxValue)]
    [DisplayName(LocalizationKeys.BaseConfiguration.MinutesUntilSwitchOff_DisplayName)]
    [Postfix("min")]
    public int MinutesUntilSwitchOff { get; set; } = 5;
    [Required]
    [DisplayName(LocalizationKeys.BaseConfiguration.PowerBuffer_DisplayName)]
    [Postfix("W")]
    [HelperText(LocalizationKeys.BaseConfiguration.PowerBuffer_HelperText)]
    public int PowerBuffer { get; set; } = 0;
    [HelperText(LocalizationKeys.BaseConfiguration.AllowPowerBufferChangeOnHome_HelperText)]
    public bool AllowPowerBufferChangeOnHome { get; set; }
    [HelperText(LocalizationKeys.BaseConfiguration.PredictSolarPowerGeneration_HelperText)]
    public bool PredictSolarPowerGeneration { get; set; }
    [HelperText(LocalizationKeys.BaseConfiguration.UsePredictedSolarPowerGenerationForChargingSchedules_HelperText)]
    public bool UsePredictedSolarPowerGenerationForChargingSchedules { get; set; }
    [HelperText(LocalizationKeys.BaseConfiguration.ShowEnergyDataOnHome_HelperText)]
    public bool ShowEnergyDataOnHome { get; set; }
    public string? CurrentPowerToGridJsonPattern { get; set; }
    public decimal CurrentPowerToGridCorrectionFactor { get; set; } = 1;
    public string? CurrentInverterPowerJsonPattern { get; set; }
    public decimal CurrentInverterPowerCorrectionFactor { get; set; } = 1;
    public string? HomeBatterySocJsonPattern { get; set; }
    public decimal HomeBatterySocCorrectionFactor { get; set; } = 1;
    public string? HomeBatteryPowerJsonPattern { get; set; }
    public decimal HomeBatteryPowerCorrectionFactor { get; set; } = 1;
    [DisplayName(LocalizationKeys.BaseConfiguration.TelegramBotKey_DisplayName)]
    public string? TelegramBotKey { get; set; }
    [DisplayName(LocalizationKeys.BaseConfiguration.TelegramChannelId_DisplayName)]
    public string? TelegramChannelId { get; set; }
    [HelperText(LocalizationKeys.BaseConfiguration.SendStackTraceToTelegram_HelperText)]
    public bool SendStackTraceToTelegram { get; set; }
    [DisplayName(LocalizationKeys.BaseConfiguration.TeslaMateDbServer_DisplayName)]
    public string? TeslaMateDbServer { get; set; }
    [DisplayName(LocalizationKeys.BaseConfiguration.TeslaMateDbPort_DisplayName)]
    [HelperText(LocalizationKeys.BaseConfiguration.TeslaMateDbPort_HelperText)]
    public int? TeslaMateDbPort { get; set; }
    [DisplayName(LocalizationKeys.BaseConfiguration.TeslaMateDbDatabaseName_DisplayName)]
    public string? TeslaMateDbDatabaseName { get; set; }
    [DisplayName(LocalizationKeys.BaseConfiguration.TeslaMateDbUser_DisplayName)]
    public string? TeslaMateDbUser { get; set; }
    [DataType(DataType.Password)]
    [DisplayName(LocalizationKeys.BaseConfiguration.TeslaMateDbPassword_DisplayName)]
    public string? TeslaMateDbPassword { get; set; }
    [DisplayName(LocalizationKeys.BaseConfiguration.MosquitoServer_DisplayName)]
    public string? MosquitoServer { get; set; }
    [Required]
    [DisplayName(LocalizationKeys.BaseConfiguration.MqqtClientId_DisplayName)]
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

    [DisplayName(LocalizationKeys.BaseConfiguration.DynamicHomeBatteryMinSoc_DisplayName)]
    [Postfix("%")]
    [HelperText(LocalizationKeys.BaseConfiguration.DynamicHomeBatteryMinSoc_HelperText)]
    public bool? DynamicHomeBatteryMinSoc { get; set; }
    [DisplayName(LocalizationKeys.BaseConfiguration.HomeBatteryMinSoc_DisplayName)]
    [Postfix("%")]
    [HelperText(LocalizationKeys.BaseConfiguration.HomeBatteryMinSoc_HelperText)]
    public int? HomeBatteryMinSoc { get; set; }
    [Postfix("%")]
    [HelperText(LocalizationKeys.BaseConfiguration.HomeBatteryMinDynamicMinSoc_HelperText)]
    public int HomeBatteryMinDynamicMinSoc { get; set; } = 5;
    [Postfix("%")]
    [HelperText(LocalizationKeys.BaseConfiguration.HomeBatteryMaxDynamicMinSoc_HelperText)]
    public int HomeBatteryMaxDynamicMinSoc { get; set; } = 95;
    [Postfix("%")]
    [HelperText(LocalizationKeys.BaseConfiguration.DynamicMinSocCalculationBuffer_HelperText)]
    public int DynamicMinSocCalculationBuffer { get; set; } = 50;
    [HelperText(LocalizationKeys.BaseConfiguration.ForceFullHomeBatteryBySunset_HelperText)]
    public bool ForceFullHomeBatteryBySunset { get; set; } = true;
    [DisplayName(LocalizationKeys.BaseConfiguration.HomeBatteryChargingPower_DisplayName)]
    [Postfix("W")]
    [HelperText(LocalizationKeys.BaseConfiguration.HomeBatteryChargingPower_HelperText)]
    public int? HomeBatteryChargingPower { get; set; }
    [DisplayName(LocalizationKeys.BaseConfiguration.HomeBatteryDischargingPower_DisplayName)]
    [Postfix("W")]
    [HelperText(LocalizationKeys.BaseConfiguration.HomeBatteryDischargingPower_HelperText)]
    public int? HomeBatteryDischargingPower { get; set; }
    [DisplayName(LocalizationKeys.BaseConfiguration.HomeBatteryUsableEnergy_DisplayName)]
    [Postfix("kWh")]
    [HelperText(LocalizationKeys.BaseConfiguration.HomeBatteryUsableEnergy_HelperText)]
    public double? HomeBatteryUsableEnergy { get; set; }
    [HelperText(LocalizationKeys.BaseConfiguration.DischargeHomeBatteryToMinSocDuringDay_HelperText)]
    public bool DischargeHomeBatteryToMinSocDuringDay { get; set; }
    [Postfix("%")]
    [HelperText(LocalizationKeys.BaseConfiguration.CarChargeLoss_HelperText)]
    public int CarChargeLoss { get; set; } = 15;
    [DisplayName(LocalizationKeys.BaseConfiguration.MaxCombinedCurrent_DisplayName)]
    [Postfix("A")]
    [HelperText(LocalizationKeys.BaseConfiguration.MaxCombinedCurrent_HelperText)]
    public int? MaxCombinedCurrent { get; set; }
    [DisplayName(LocalizationKeys.BaseConfiguration.MaxInverterAcPower_DisplayName)]
    [Postfix("W")]
    [HelperText(LocalizationKeys.BaseConfiguration.MaxInverterAcPower_HelperText)]
    public int? MaxInverterAcPower { get; set; }
    public string? BleApiBaseUrl { get; set; }
    [DisplayName(LocalizationKeys.BaseConfiguration.UseTeslaMateIntegration_DisplayName)]
    [HelperText(LocalizationKeys.BaseConfiguration.UseTeslaMateIntegration_HelperText)]
    public bool UseTeslaMateIntegration { get; set; }
    [DisplayName(LocalizationKeys.BaseConfiguration.UseTeslaMateAsDataSource_DisplayName)]
    [HelperText(LocalizationKeys.BaseConfiguration.UseTeslaMateAsDataSource_HelperText)]
    public bool UseTeslaMateAsDataSource { get; set; }
    public double HomeGeofenceLongitude { get; set; } = 13.3761736; //Do not change the default value as depending on this the Geofence from TeslaMate is converted or not
    public double HomeGeofenceLatitude { get; set; } = 52.5185238; //Do not change the default value as depending on this the Geofence from TeslaMate is converted or not
    [DisplayName(LocalizationKeys.BaseConfiguration.HomeGeofenceRadius_DisplayName)]
    [Postfix("m")]
    [HelperText(LocalizationKeys.BaseConfiguration.HomeGeofenceRadius_HelperText)]
    public int HomeGeofenceRadius { get; set; } = 50;

    public FrontendConfiguration? FrontendConfiguration { get; set; }

}
