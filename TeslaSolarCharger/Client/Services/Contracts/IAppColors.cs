using TeslaSolarCharger.Client.Services.Contracts;

namespace TeslaSolarCharger.Client.Services.Contracts;

public interface IAppColors
{
    string PrimaryColor { get; }
    string SecondaryColor { get; }
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
