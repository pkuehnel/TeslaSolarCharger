using TeslaSolarCharger.Client.Services.Contracts;

namespace TeslaSolarCharger.Client.Services;

public class AppColors : IAppColors
{
    private readonly IThemeStateService _themeStateService;

    public AppColors(IThemeStateService themeStateService)
    {
        _themeStateService = themeStateService;
    }

    public string PrimaryColor => "#1b6ec2";
    public string SecondaryColor => "#6c757d";
    public string SolarPowerColor => "#F57C00";
    public string SolarPowerPredictionColor => "#FFB300";
    public string GridColor => PrimaryColor;
    public string GridExportColor => "#4CAF50";
    public string GridImportColor => "#D32F2F";
    public string BatteryColor => SecondaryColor;
    public string BatteryChargingColor => "#20B2AA";
    public string BatteryDischargingColor => "#FF6347";
    public string BatterySocColor => "#212121";
    public string HomeConsumptionColor => "#FF689D";
    public string HomeConsumptionChartColor => "#FF689D";
    public string HomeConsumptionPredictionColor => "#FFE6E9";
    public string EvChargingColor => "#939393";
    public string ConsumptionColor => "#ff6d59";
    public string FeedInColor => "#55cf55";
    public string DarkModeBackgroundColor => "#212529";

    // Battery SOC Icon colors
    public string BatterySocGoodColor => "#008000aa";

    public string BatterySocWarningColor => _themeStateService.IsDarkMode ? "#ccca00aa" : "#ffff00aa";

    public string BatterySocCriticalColor => "red";
}
