using Microsoft.AspNetCore.Mvc;
using MudBlazor;
using Newtonsoft.Json;
using System.Net.Http.Json;
using TeslaSolarCharger.Client.Helper.Contracts;

namespace TeslaSolarCharger.Client.Helper;

public class HttpClientHelper(HttpClient httpClient, ISnackbar snackbar, IDialogHelper dialogHelper) : IHttpClientHelper
{
    public async Task<T?> SendGetRequestWithSnackbarAsync<T>(string url)
    {
        return await SendRequestWithSnackbarInternalAsync<T>(HttpMethod.Get, url, null);
    }

    public async Task SendGetRequestWithSnackbarAsync(string url)
    {
        await SendRequestWithSnackbarInternalAsync<object>(HttpMethod.Get, url, null);
    }

    public async Task<T?> SendPostRequestWithSnackbarAsync<T>(string url, object? content)
    {
        return await SendRequestWithSnackbarInternalAsync<T>(HttpMethod.Post, url, content);
    }

    public async Task SendPostRequestWithSnackbarAsync(string url, object? content)
    {
        await SendRequestWithSnackbarInternalAsync<object>(HttpMethod.Post, url, content);
    }

    private async Task<T?> SendRequestWithSnackbarInternalAsync<T>(
        HttpMethod method,
        string url,
        object? content)
    {
        try
        {
            HttpResponseMessage response;
            if (method == HttpMethod.Get)
            {
                response = await httpClient.GetAsync(url);
            }
            else if (method == HttpMethod.Post)
            {
                var jsonContent = new StringContent(
                    JsonConvert.SerializeObject(content),
                    System.Text.Encoding.UTF8,
                    "application/json");
                response = await httpClient.PostAsync(url, jsonContent);
            }
            else
            {
                throw new ArgumentException("Unsupported HTTP method", nameof(method));
            }

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                if (typeof(T) != typeof(object))
                {
                    var deserializedObject = JsonConvert.DeserializeObject<T>(responseContent);
                    if (deserializedObject == null)
                    {
                        snackbar.Add($"{url}: The string could not be deserialized to the object type.", Severity.Error);
                    }
                    return deserializedObject;
                }

                if (string.IsNullOrEmpty(responseContent))
                {
                    return default;
                }
                snackbar.Add($"{url}: The specified object type is not supported", Severity.Error);
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
                var message = $"{url}: Unexpected error: {response.StatusCode}";
                snackbar.Add(message, Severity.Error);
            }
        }
        catch (HttpRequestException ex)
        {
            var message = $"{url}: Network error: {ex.Message}";
            snackbar.Add(message, Severity.Error);
        }
        catch (Exception ex)
        {
            var message = $"{url}: Unexpected error: {ex.Message}";
            snackbar.Add(message, Severity.Error, config =>
            {
                config.Action = "Details";
                config.ActionColor = Color.Primary;
                config.Onclick = snackbar1 => dialogHelper.ShowTextDialog("Error Details",
                    $"Unexpected error while calling {url}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            });
        }
        return default;
    }
}
