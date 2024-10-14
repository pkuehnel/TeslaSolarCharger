using MudBlazor;
using TeslaSolarCharger.Client.Dialogs;
using TeslaSolarCharger.Client.Helper.Contracts;

namespace TeslaSolarCharger.Client.Helper;

public class DialogHelper(IDialogService dialogService) : IDialogHelper
{
    public async Task ShowTextDialog(string title, string dialogText)
    {
        var options = new DialogOptions()
        {
            CloseButton = true,
            CloseOnEscapeKey = true,
        };
        var parameters = new DialogParameters<TextDialog>
        {
            { x => x.Text, dialogText },
        };
        var dialog = await dialogService.ShowAsync<TextDialog>(title, parameters, options);
        var result = await dialog.Result;
    }
}
