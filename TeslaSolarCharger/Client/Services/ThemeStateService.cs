using TeslaSolarCharger.Client.Services.Contracts;

namespace TeslaSolarCharger.Client.Services;

public class ThemeStateService : IThemeStateService
{
    public bool IsDarkMode { get; private set; }

    public event Func<bool, Task>? OnDarkModeChanged;

    public async Task SetDarkModeAsync(bool isDarkMode)
    {
        if (IsDarkMode != isDarkMode)
        {
            IsDarkMode = isDarkMode;

            if (OnDarkModeChanged != null)
            {
                // Grab all subscribers to the event
                var handlers = OnDarkModeChanged.GetInvocationList().Cast<Func<bool, Task>>();

                // Invoke all of them and collect their tasks
                var tasks = handlers.Select(handler => handler(isDarkMode));

                // Await all tasks to ensure none are fired-and-forgotten
                await Task.WhenAll(tasks);
            }
        }
    }
}
