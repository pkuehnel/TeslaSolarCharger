using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using MudBlazor;
using TeslaSolarCharger.Client.Dialogs;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Client.Services;

public class OAuthNotificationService(
    NavigationManager navigationManager,
    ISnackbar snackbar,
    IDialogService dialogService,
    IHttpClientHelper httpClientHelper,
    IConstants constants) : IOAuthNotificationService
{
    public async Task HandleQueryParameters()
    {
        var uri = navigationManager.ToAbsoluteUri(navigationManager.Uri);
        var query = QueryHelpers.ParseQuery(uri.Query);

        bool handled = false;
        if (query.TryGetValue(constants.QueryParamError, out var error))
        {
            var parameters = new DialogParameters<TextDialog> { { x => x.Text, error.ToString()! } };
            await dialogService.ShowAsync<TextDialog>("Error", parameters);
            handled = true;
        }

        if (query.TryGetValue(constants.QueryParamSuccess, out var success) && success == "true")
        {
            handled = true;
            if (query.TryGetValue(constants.QueryParamVin, out var vin))
            {
                var result = await httpClientHelper.SendPostRequestAsync<object>($"/api/BackendApi/ConnectCarToSmartCar?vin={Uri.EscapeDataString(vin.ToString())}", null).ConfigureAwait(false);
                if (result.HasError)
                {
                    snackbar.Add("Failed to confirm SmartCar connection: " + result.ErrorMessage, Severity.Error);
                }
                else if (query.TryGetValue(constants.QueryParamMessage, out var message))
                {
                    snackbar.Add(message.ToString(), Severity.Success);
                }
            }
            else if (query.TryGetValue(constants.QueryParamMessage, out var message))
            {
                snackbar.Add(message.ToString(), Severity.Success);
            }
        }

        if (handled)
        {
            var newUri = navigationManager.GetUriWithQueryParameters(new Dictionary<string, object?>
            {
                [constants.QueryParamError] = null,
                [constants.QueryParamSuccess] = null,
                [constants.QueryParamVin] = null,
                [constants.QueryParamMessage] = null,
            });
            navigationManager.NavigateTo(newUri);
        }
    }
}
