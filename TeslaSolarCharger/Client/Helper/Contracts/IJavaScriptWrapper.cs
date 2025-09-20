namespace TeslaSolarCharger.Client.Helper.Contracts;

public interface IJavaScriptWrapper
{
    Task<bool> SetFocusToElementById(string elementId);
    Task<bool> RemoveFocusFromElementById(string elementId);
    Task<bool> ScrollToElementById(string elementId);
    Task OpenUrlInNewTab(string url);
    Task<bool> IsIosDevice();
    Task<string> GetTimeZoneId();
    Task CopyStringToClipboard(string inputString);

    /// <summary>
    /// Reloads the current page with a hard refresh, forcing all resources including WebAssembly files to be redownloaded.
    /// </summary>
    Task ReloadPage();

    /// <summary>
    /// Saves a string value to browser local storage
    /// </summary>
    /// <param name="key">The key to store the value under</param>
    /// <param name="value">The string value to store</param>
    Task SaveToLocalStorage(string key, string value);

    /// <summary>
    /// Reads a string value from browser local storage
    /// </summary>
    /// <param name="key">The key to retrieve the value for</param>
    /// <returns>The stored string value, or null if not found</returns>
    Task<string?> ReadFromLocalStorage(string key);
}
