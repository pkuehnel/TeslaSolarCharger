namespace TeslaSolarCharger.Client.Services.Contracts;

public interface IThemeStateService
{
    bool IsDarkMode { get; }
    event Func<bool, Task>? OnDarkModeChanged;
    void SetDarkMode(bool isDarkMode);
}
