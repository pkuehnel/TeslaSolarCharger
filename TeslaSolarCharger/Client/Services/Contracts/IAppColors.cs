namespace TeslaSolarCharger.Client.Services.Contracts;

public interface IAppColors
{
    // ----- Brand palette tokens (drive the MudTheme; see MainLayout) -----
    string PrimaryColor { get; }
    string PrimaryDarkenColor { get; }
    string PrimaryLightenColor { get; }
    string SecondaryColor { get; }
    string TertiaryColor { get; }
    string InkColor { get; }
    string AppBarBackgroundColor { get; }
    string PageBackgroundColor { get; }
    string DarkModeSurfaceColor { get; }
    string DarkModeAppBarColor { get; }

    // ----- Energy / chart colors -----
    string SolarPowerColor { get; }
    string ConsumptionColor { get; }
    string FeedInColor { get; }
    string GridColor { get; }
    string BatteryColor { get; }
    string EvChargingColor { get; }
    string HomeConsumptionColor { get; }
    string SolarPowerPredictionColor { get; }
    string HomeConsumptionPredictionColor { get; }
    string BatterySocColor { get; }
    string HomeConsumptionChartColor { get; }
    string GridExportColor { get; }
    string GridImportColor { get; }
    string BatteryChargingColor { get; }
    string BatteryDischargingColor { get; }
    string DarkModeBackgroundColor { get; }

    // Battery SOC Icon colors
    string BatterySocGoodColor { get; }
    string BatterySocWarningColor { get; }
    string BatterySocCriticalColor { get; }
}
