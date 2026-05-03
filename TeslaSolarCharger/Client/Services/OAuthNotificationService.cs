using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using MudBlazor;
using TeslaSolarCharger.Client.Dialogs;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared.Localization;
using TeslaSolarCharger.Shared.Localization.Contracts;
using TeslaSolarCharger.Shared.Localization.Registries;
using TeslaSolarCharger.Shared.Localization.Registries.Pages;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Client.Services;

public class OAuthNotificationService(
    NavigationManager navigationManager,
    ISnackbar snackbar,
    IDialogService dialogService,
    IHttpClientHelper httpClientHelper,
    IConstants constants,
    ITextLocalizationService textLocalizer) : IOAuthNotificationService
{
    private string T(string key) =>
        textLocalizer.Get<CarSettingsPageLocalizationRegistry>(key, typeof(SharedComponentLocalizationRegistry))
        ?? key;

    private string TF(string key, params object[] args) =>
        textLocalizer.GetFormat<CarSettingsPageLocalizationRegistry>(key, args, typeof(SharedComponentLocalizationRegistry));

    public async Task HandleQueryParameters()
    {
        var uri = navigationManager.ToAbsoluteUri(navigationManager.Uri);
        var query = QueryHelpers.ParseQuery(uri.Query);

        bool handled = false;
        if (query.TryGetValue(constants.QueryParamError, out var error))
        {
            var parameters = new DialogParameters<TextDialog> { { x => x.Text, error.ToString()! } };
            await dialogService.ShowAsync<TextDialog>(T(TranslationKeys.GeneralErrorTitle), parameters);
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
                    snackbar.Add(TF(TranslationKeys.OAuthNotificationSmartCarConnectionFailed, result.ErrorMessage ?? string.Empty), Severity.Error);
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
