using System.Globalization;

namespace TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

public class DtoBaseConfiguration : BaseConfigurationBase
{
    private string? _currentPowerToGridCorrectionFactorString;
    public string CurrentPowerToGridCorrectionFactorString
    {
        get => CurrentPowerToGridCorrectionFactor.ToString(CultureInfo.InvariantCulture);
        set
        {
            _currentPowerToGridCorrectionFactorString = value.Replace(",", ".");
            CurrentPowerToGridCorrectionFactor = Convert.ToDecimal(_currentPowerToGridCorrectionFactorString);
        }
    }

    private string? _currentInverterPowerCorrectionFactorString;
    public string CurrentInverterPowerCorrectionFactorString
    {
        get => CurrentInverterPowerCorrectionFactor.ToString(CultureInfo.InvariantCulture);
        set
        {
            _currentInverterPowerCorrectionFactorString = value.Replace(",", ".");
            CurrentInverterPowerCorrectionFactor = Convert.ToDecimal(_currentInverterPowerCorrectionFactorString);
        }
    }
}