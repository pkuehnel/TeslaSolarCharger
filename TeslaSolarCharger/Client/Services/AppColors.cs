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
    public string HomeConsumptionColor => _themeStateService.IsDarkMode ? "#bf285d" : "#ff689d";
    public string HomeConsumptionChartColor => HomeConsumptionColor;
    public string HomeConsumptionPredictionColor => "#FFE6E9";
    public string EvChargingColor => _themeStateService.IsDarkMode ? "#636363" : "#d3d3d3";
    public string ConsumptionColor => _themeStateService.IsDarkMode ? "#780606" : "#FF8C7C";
    public string FeedInColor => _themeStateService.IsDarkMode ? "#00a000" : "90ee90";
    public string DarkModeBackgroundColor => "#212529";

    // Battery SOC Icon colors
    public string BatterySocGoodColor => _themeStateService.IsDarkMode ? "#008000" : "#00c000";

    public string BatterySocWarningColor => _themeStateService.IsDarkMode ? "#ccca00" : "#ffff00";

    public string BatterySocCriticalColor => "red";
}
