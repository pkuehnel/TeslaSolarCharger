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
}
