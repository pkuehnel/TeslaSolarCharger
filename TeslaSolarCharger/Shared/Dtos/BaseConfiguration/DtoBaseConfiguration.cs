using System.Globalization;

namespace TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

public class DtoBaseConfiguration : BaseConfigurationBase
{
    private string? _currentPowerToGridCorrectionFactorString;
    public string CurrentPowerToGridCorrectionFactorString
    {
        get => CurrentPowerToGridCorrectionFactor.ToString(new CultureInfo("en-US"));
        set
        {
            _currentPowerToGridCorrectionFactorString = value.Replace(",", ".");
            CurrentPowerToGridCorrectionFactor = Convert.ToDecimal(_currentPowerToGridCorrectionFactorString, new CultureInfo("en-US"));
        }
    }

    private string? _currentInverterPowerCorrectionFactorString;
    public string CurrentInverterPowerCorrectionFactorString
    {
        get => CurrentInverterPowerCorrectionFactor.ToString(new CultureInfo("en-US"));
        set
        {
            _currentInverterPowerCorrectionFactorString = value.Replace(",", ".");
            CurrentInverterPowerCorrectionFactor = Convert.ToDecimal(_currentInverterPowerCorrectionFactorString, new CultureInfo("en-US"));
        }
    }

    private string? _homeBatterySocCorrectionFactorString;
    public string HomeBatterySocCorrectionFactorString
    {
        get => HomeBatterySocCorrectionFactor.ToString(new CultureInfo("en-US"));
        set
        {
            _homeBatterySocCorrectionFactorString = value.Replace(",", ".");
            HomeBatterySocCorrectionFactor = Convert.ToDecimal(_homeBatterySocCorrectionFactorString, new CultureInfo("en-US"));
        }
    }

    private string? _homeBatteryPowerCorrectionFactorString;
    public string HomeBatteryPowerCorrectionFactorString
    {
        get => HomeBatteryPowerCorrectionFactor.ToString(new CultureInfo("en-US"));
        set
        {
            _homeBatteryPowerCorrectionFactorString = value.Replace(",", ".");
            HomeBatteryPowerCorrectionFactor = Convert.ToDecimal(_homeBatteryPowerCorrectionFactorString, new CultureInfo("en-US"));
        }
    }
}
