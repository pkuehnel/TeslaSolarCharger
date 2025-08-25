namespace TeslaSolarCharger.Client.Helper.Contracts;

public interface IJavaScriptWrapper
{
    Task<bool> SetFocusToElementById(string elementId);
    Task<bool> RemoveFocusFromElementById(string elementId);
    Task OpenUrlInNewTab(string url);
    Task<bool> IsIosDevice();
    Task<string> GetTimeZoneId();
    Task CopyStringToClipboard(string inputString);

    /// <summary>
    /// Reloads the current page with a hard refresh, forcing all resources including WebAssembly files to be redownloaded.
    /// </summary>
    Task ReloadPage();
}
