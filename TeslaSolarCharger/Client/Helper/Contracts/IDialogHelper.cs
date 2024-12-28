using MudBlazor;

namespace TeslaSolarCharger.Client.Helper.Contracts;

public interface IDialogHelper
{
    Task ShowTextDialog(string title, string dialogText);
    Task<bool> ShowCreateBackendTokenDialog();
}
