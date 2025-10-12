using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TeslaSolarCharger.Shared.Attributes;
using TeslaSolarCharger.Shared.Localization.TextCatalog;

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
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.HomeBatteryPowerInversionUrl))]
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.HomeBatteryPowerInversionUrlHelper))]
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
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.UpdateIntervalSeconds))]
    [Postfix("s")]
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.UpdateIntervalSecondsHelper))]
    public int UpdateIntervalSeconds { get; set; } = 30;
    [Required]
    [Postfix("s")]
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.SkipPowerChangesOnLastAdjustmentNewerThanSecondsHelper))]
    public int SkipPowerChangesOnLastAdjustmentNewerThanSeconds { get; set; } = 25;
    [Required]
    [Range(1, int.MaxValue)]
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.PvValueUpdateIntervalSeconds))]
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
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.MinutesUntilSwitchOn))]
    [Postfix("min")]
    public int MinutesUntilSwitchOn { get; set; } = 5;
    [Required]
    [Range(1, int.MaxValue)]
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.MinutesUntilSwitchOff))]
    [Postfix("min")]
    public int MinutesUntilSwitchOff { get; set; } = 5;
    [Required]
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.PowerBuffer))]
    [Postfix("W")]
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.PowerBufferHelper))]
    public int PowerBuffer { get; set; } = 0;
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.AllowPowerBufferChangeOnHomeHelper))]
    public bool AllowPowerBufferChangeOnHome { get; set; }
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.PredictSolarPowerGenerationHelper))]
    public bool PredictSolarPowerGeneration { get; set; }
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.UsePredictedSolarPowerGenerationForChargingSchedulesHelper))]
    public bool UsePredictedSolarPowerGenerationForChargingSchedules { get; set; }
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.ShowEnergyDataOnHomeHelper))]
    public bool ShowEnergyDataOnHome { get; set; }
    public string? CurrentPowerToGridJsonPattern { get; set; }
    public decimal CurrentPowerToGridCorrectionFactor { get; set; } = 1;
    public string? CurrentInverterPowerJsonPattern { get; set; }
    public decimal CurrentInverterPowerCorrectionFactor { get; set; } = 1;
    public string? HomeBatterySocJsonPattern { get; set; }
    public decimal HomeBatterySocCorrectionFactor { get; set; } = 1;
    public string? HomeBatteryPowerJsonPattern { get; set; }
    public decimal HomeBatteryPowerCorrectionFactor { get; set; } = 1;
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.TelegramBotKey))]
    public string? TelegramBotKey { get; set; }
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.TelegramChannelId))]
    public string? TelegramChannelId { get; set; }
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.SendStackTraceToTelegramHelper))]
    public bool SendStackTraceToTelegram { get; set; }
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.TeslaMateDbServer))]
    public string? TeslaMateDbServer { get; set; }
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.TeslaMateDbPort))]
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.TeslaMateDbPortHelper))]
    public int? TeslaMateDbPort { get; set; }
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.TeslaMateDbDatabaseName))]
    public string? TeslaMateDbDatabaseName { get; set; }
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.TeslaMateDbUser))]
    public string? TeslaMateDbUser { get; set; }
    [DataType(DataType.Password)]
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.TeslaMateDbPassword))]
    public string? TeslaMateDbPassword { get; set; }
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.MosquitoServer))]
    public string? MosquitoServer { get; set; }
    [Required]
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.MqqtClientId))]
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

    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.DynamicHomeBatteryMinSoc))]
    [Postfix("%")]
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.DynamicHomeBatteryMinSocHelper))]
    public bool? DynamicHomeBatteryMinSoc { get; set; }
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.HomeBatteryMinSoc))]
    [Postfix("%")]
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.HomeBatteryMinSocHelper))]
    public int? HomeBatteryMinSoc { get; set; }
    [Postfix("%")]
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.HomeBatteryMinDynamicMinSocHelper))]
    public int HomeBatteryMinDynamicMinSoc { get; set; } = 5;
    [Postfix("%")]
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.HomeBatteryMaxDynamicMinSocHelper))]
    public int HomeBatteryMaxDynamicMinSoc { get; set; } = 95;
    [Postfix("%")]
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.DynamicMinSocCalculationBufferHelper))]
    public int DynamicMinSocCalculationBuffer { get; set; } = 50;
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.ForceFullHomeBatteryBySunsetHelper))]
    public bool ForceFullHomeBatteryBySunset { get; set; } = true;
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.HomeBatteryChargingPower))]
    [Postfix("W")]
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.HomeBatteryChargingPowerHelper))]
    public int? HomeBatteryChargingPower { get; set; }
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.HomeBatteryDischargingPower))]
    [Postfix("W")]
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.HomeBatteryDischargingPowerHelper))]
    public int? HomeBatteryDischargingPower { get; set; }
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.HomeBatteryUsableEnergy))]
    [Postfix("kWh")]
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.HomeBatteryUsableEnergyHelper))]
    public double? HomeBatteryUsableEnergy { get; set; }
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.DischargeHomeBatteryToMinSocDuringDayHelper))]
    public bool DischargeHomeBatteryToMinSocDuringDay { get; set; }
    [Postfix("%")]
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.CarChargeLossHelper))]
    public int CarChargeLoss { get; set; } = 15;
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.MaxCombinedCurrent))]
    [Postfix("A")]
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.MaxCombinedCurrentHelper))]
    public int? MaxCombinedCurrent { get; set; }
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.MaxInverterAcPower))]
    [Postfix("W")]
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.MaxInverterAcPowerHelper))]
    public int? MaxInverterAcPower { get; set; }
    public string? BleApiBaseUrl { get; set; }
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.UseTeslaMateIntegration))]
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.UseTeslaMateIntegrationHelper))]
    public bool UseTeslaMateIntegration { get; set; }
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.UseTeslaMateAsDataSource))]
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.UseTeslaMateAsDataSourceHelper))]
    public bool UseTeslaMateAsDataSource { get; set; }
    public double HomeGeofenceLongitude { get; set; } = 13.3761736;
    public double HomeGeofenceLatitude { get; set; } = 52.5185238;
    [LocalizedDisplayName(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.HomeGeofenceRadius))]
    [Postfix("m")]
    [HelperText(typeof(BaseConfigurationTexts), nameof(BaseConfigurationTexts.HomeGeofenceRadiusHelper))]
    public int HomeGeofenceRadius { get; set; } = 50;

    public FrontendConfiguration? FrontendConfiguration { get; set; }
}
