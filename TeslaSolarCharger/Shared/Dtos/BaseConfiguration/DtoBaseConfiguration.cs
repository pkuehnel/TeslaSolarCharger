using System.Globalization;

namespace TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

public class DtoBaseConfiguration : BaseConfigurationBase
{
    private string? _currentPowerToGridCorrectionFactorString;
    public string CurrentPowerToGridCorrectionFactorString
    {
        get
        {
            return CurrentPowerToGridCorrectionFactor.ToString(CultureInfo.InvariantCulture);
        }
        set
        {
            _currentPowerToGridCorrectionFactorString = value.Replace(",", ".");
            CurrentPowerToGridCorrectionFactor = Convert.ToDecimal(_currentPowerToGridCorrectionFactorString);
        }
    }
}