namespace TeslaSolarCharger.Client.Services.Contracts;

public interface IThemeStateService
{
    bool IsDarkMode { get; }
    event Action<bool>? OnDarkModeChanged;
    void SetDarkMode(bool isDarkMode);
}
