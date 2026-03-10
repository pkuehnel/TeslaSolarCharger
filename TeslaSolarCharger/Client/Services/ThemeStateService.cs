using TeslaSolarCharger.Client.Services.Contracts;

namespace TeslaSolarCharger.Client.Services;

public class ThemeStateService : IThemeStateService
{
    public bool IsDarkMode { get; private set; }

    public event Action<bool>? OnDarkModeChanged;

    public void SetDarkMode(bool isDarkMode)
    {
        if (IsDarkMode != isDarkMode)
        {
            IsDarkMode = isDarkMode;
            OnDarkModeChanged?.Invoke(isDarkMode);
        }
    }
}
