using System.ComponentModel.DataAnnotations;
using TeslaSolarCharger.Shared.Attributes;

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
    [Postfix("s")]
    public int UpdateIntervalSeconds { get; set; } = 30;
    [Required]
    [Postfix("s")]
    public int SkipPowerChangesOnLastAdjustmentNewerThanSeconds { get; set; } = 25;
    [Required]
    [Range(1, int.MaxValue)]
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
    [Postfix("min")]
    public int MinutesUntilSwitchOn { get; set; } = 5;
    [Required]
    [Range(1, int.MaxValue)]
    [Postfix("min")]
    public int MinutesUntilSwitchOff { get; set; } = 5;
    [Required]
    [Postfix("W")]
    public int PowerBuffer { get; set; } = 0;
    public bool AllowPowerBufferChangeOnHome { get; set; }
    public bool PredictSolarPowerGeneration { get; set; }
    public bool UsePredictedSolarPowerGenerationForChargingSchedules { get; set; }
    public bool ShowEnergyDataOnHome { get; set; }
    public string? CurrentPowerToGridJsonPattern { get; set; }
    public decimal CurrentPowerToGridCorrectionFactor { get; set; } = 1;
    public string? CurrentInverterPowerJsonPattern { get; set; }
    public decimal CurrentInverterPowerCorrectionFactor { get; set; } = 1;
    public string? HomeBatterySocJsonPattern { get; set; }
    public decimal HomeBatterySocCorrectionFactor { get; set; } = 1;
    public string? HomeBatteryPowerJsonPattern { get; set; }
    public decimal HomeBatteryPowerCorrectionFactor { get; set; } = 1;
    public string? TelegramBotKey { get; set; }
    public string? TelegramChannelId { get; set; }
    public bool SendStackTraceToTelegram { get; set; }
    public string? TeslaMateDbServer { get; set; }
    public int? TeslaMateDbPort { get; set; }
    public string? TeslaMateDbDatabaseName { get; set; }
    public string? TeslaMateDbUser { get; set; }
    [DataType(DataType.Password)]
    public string? TeslaMateDbPassword { get; set; }
    public string? MosquitoServer { get; set; }
    [Required]
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
    [Postfix("%")]
    public bool? DynamicHomeBatteryMinSoc { get; set; }
    [Postfix("%")]
    public int? HomeBatteryMinSoc { get; set; }
    [Postfix("%")]
    public int HomeBatteryMinDynamicMinSoc { get; set; } = 5;
    [Postfix("%")]
    public int HomeBatteryMaxDynamicMinSoc { get; set; } = 95;
    [Postfix("%")]
    public int DynamicMinSocCalculationBuffer { get; set; } = 50;
    public bool ForceFullHomeBatteryBySunset { get; set; } = true;
    [Postfix("W")]
    public int? HomeBatteryChargingPower { get; set; }
    [Postfix("W")]
    public int? HomeBatteryDischargingPower { get; set; }
    [Postfix("kWh")]
    public double? HomeBatteryUsableEnergy { get; set; }
    public bool DischargeHomeBatteryToMinSocDuringDay { get; set; }
    [Postfix("%")]
    public int CarChargeLoss { get; set; } = 15;
    [Postfix("A")]
    public int? MaxCombinedCurrent { get; set; }
    [Postfix("W")]
    public int? MaxInverterAcPower { get; set; }
    public string? BleApiBaseUrl { get; set; }
    public bool UseTeslaMateIntegration { get; set; }
    public bool UseTeslaMateAsDataSource { get; set; }
    public double HomeGeofenceLongitude { get; set; } = 13.3761736; //Do not change the default value as depending on this the Geofence from TeslaMate is converted or not
    public double HomeGeofenceLatitude { get; set; } = 52.5185238; //Do not change the default value as depending on this the Geofence from TeslaMate is converted or not
    [Postfix("m")]
    public int HomeGeofenceRadius { get; set; } = 50;

    public FrontendConfiguration? FrontendConfiguration { get; set; }

}
