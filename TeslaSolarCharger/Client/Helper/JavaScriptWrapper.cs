using Lysando.LabStorageV2.UiHelper.Wrapper.Contracts;
using Microsoft.JSInterop;

namespace Lysando.LabStorageV2.UiHelper.Wrapper;

public class JavaScriptWrapper(IJSRuntime jsRuntime) : IJavaScriptWrapper
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
}