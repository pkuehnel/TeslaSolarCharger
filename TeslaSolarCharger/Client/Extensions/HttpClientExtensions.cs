using Microsoft.AspNetCore.Mvc;
using MudBlazor;
using Newtonsoft.Json;
using System.Net.Http.Json;

namespace TeslaSolarCharger.Client.Extensions;

public static class HttpClientExtensions
{
    public static async Task<T?> SendGetRequestWithSnackbarAsync<T>(
        this HttpClient httpClient,
        string url,
        ISnackbar snackbar)
    {
        return await SendGetRequestWithSnackbarInternalAsync<T>(httpClient, url, snackbar);
    }

    public static async Task SendGetRequestWithSnackbarAsync(
        this HttpClient httpClient,
        string url,
        ISnackbar snackbar)
    {
        await SendGetRequestWithSnackbarInternalAsync<object>(httpClient, url, snackbar);
    }

    private static async Task<T?> SendGetRequestWithSnackbarInternalAsync<T>(
        HttpClient httpClient,
        string url,
        ISnackbar snackbar)
    {
        try
        {
            var response = await httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();

                if (typeof(T) != typeof(object))
                {
                    var deserializedObject = JsonConvert.DeserializeObject<T>(content);
                    if (deserializedObject == null)
                    {
                        snackbar.Add("The string could not be deserialized to the obejct type.", Severity.Error);
                    }
                    return deserializedObject;
                }

                snackbar.Add("The specified object type is not supported", Severity.Error);
                return default;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
            {
                var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
                var message = problemDetails != null ? $"Error: {problemDetails.Detail}" : "An error occurred";
                snackbar.Add(message, Severity.Error);
            }
            else
            {
                var message = $"Unexpected error: {response.StatusCode}";
                snackbar.Add(message, Severity.Error);
            }
        }
        catch (HttpRequestException ex)
        {
            var message = $"Network error: {ex.Message}";
            snackbar.Add(message, Severity.Error);
        }
        catch (Exception ex)
        {
            var message = $"Unexpected error: {ex.Message}";
            snackbar.Add(message, Severity.Error);
        }

        return default;
    }
}
