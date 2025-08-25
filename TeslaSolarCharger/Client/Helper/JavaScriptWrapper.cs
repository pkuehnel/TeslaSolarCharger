using Microsoft.JSInterop;
using MudBlazor;
using TeslaSolarCharger.Client.Helper.Contracts;

namespace TeslaSolarCharger.Client.Helper;

public class JavaScriptWrapper(IJSRuntime jsRuntime, ISnackbar snackbar) : IJavaScriptWrapper
{
    /// <summary>
    /// Sets the focus to an element with a specific ID
    /// </summary>
    /// <param name="elementId">ID to set the focus on</param>
    /// <returns>Was the ID set successfully</returns>
    public async Task<bool> SetFocusToElementById(string elementId)
    {
        try
        {
            return await jsRuntime.InvokeAsync<bool>("setFocus", elementId);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> RemoveFocusFromElementById(string elementId)
    {
        try
        {
            return await jsRuntime.InvokeAsync<bool>("removeFocus", elementId);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task OpenUrlInNewTab(string url)
    {
        await jsRuntime.InvokeVoidAsync("openInNewTab", url);
    }

    public async Task<bool> IsIosDevice()
    {
        try
        {
            var device = await jsRuntime.InvokeAsync<string>("detectDevice");
            return device == "iOS";
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<string> GetTimeZoneId()
    {
        var timeZone = await jsRuntime.InvokeAsync<string>("getTimeZone");
        return timeZone;
    }

    public async Task CopyStringToClipboard(string inputString)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("copyToClipboard", inputString);
            snackbar.Add("Copied to clipboard.", Severity.Success);
            
        }
        catch (Exception e)
        {
            snackbar.Add("Failed to copy to clipboard.", Severity.Error);
        }
    }

    /// <summary>
    /// Reloads the current page with a hard refresh, forcing all resources including WebAssembly files to be redownloaded.
    /// </summary>
    public async Task ReloadPage()
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("forceHardReload");
        }
        catch (Exception e)
        {
            snackbar.Add("Failed to reload the page.", Severity.Error);
        }
    }

    /// <summary>
    /// Saves a string value to browser local storage
    /// </summary>
    /// <param name="key">The key to store the value under</param>
    /// <param name="value">The string value to store</param>
    public async Task SaveToLocalStorage(string key, string value)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("saveToLocalStorage", key, value);
        }
        catch (Exception e)
        {
            snackbar.Add($"Failed to save to local storage: {e.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// Reads a string value from browser local storage
    /// </summary>
    /// <param name="key">The key to retrieve the value for</param>
    /// <returns>The stored string value, or null if not found</returns>
    public async Task<string?> ReadFromLocalStorage(string key)
    {
        try
        {
            var result = await jsRuntime.InvokeAsync<string?>("readFromLocalStorage", key);
            return result;
        }
        catch (Exception e)
        {
            snackbar.Add($"Failed to read from local storage: {e.Message}", Severity.Error);
            return null;
        }
    }
}
