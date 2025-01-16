namespace TeslaSolarCharger.Client.Helper.Contracts;

public interface IJavaScriptWrapper
{
    Task<bool> SetFocusToElementById(string elementId);
    Task<bool> RemoveFocusFromElementById(string elementId);
    Task OpenUrlInNewTab(string url);
    Task<bool> IsIosDevice();
}
