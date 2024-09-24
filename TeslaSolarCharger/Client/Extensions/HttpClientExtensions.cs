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
        return await SendRequestWithSnackbarInternalAsync<T>(httpClient, HttpMethod.Get, url, null, snackbar);
    }

    public static async Task SendGetRequestWithSnackbarAsync(
        this HttpClient httpClient,
        string url,
        ISnackbar snackbar)
    {
        await SendRequestWithSnackbarInternalAsync<object>(httpClient, HttpMethod.Get, url, null, snackbar);
    }

    public static async Task<T?> SendPostRequestWithSnackbarAsync<T>(
        this HttpClient httpClient,
        string url,
        object content,
        ISnackbar snackbar)
    {
        return await SendRequestWithSnackbarInternalAsync<T>(httpClient, HttpMethod.Post, url, content, snackbar);
    }

    public static async Task SendPostRequestWithSnackbarAsync(
        this HttpClient httpClient,
        string url,
        object content,
        ISnackbar snackbar)
    {
        await SendRequestWithSnackbarInternalAsync<object>(httpClient, HttpMethod.Post, url, content, snackbar);
    }

    private static async Task<T?> SendRequestWithSnackbarInternalAsync<T>(
        HttpClient httpClient,
        HttpMethod method,
        string url,
        object? content,
        ISnackbar snackbar)
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
                var jsonContent = new StringContent(JsonConvert.SerializeObject(content), System.Text.Encoding.UTF8, "application/json");
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
                        snackbar.Add("The string could not be deserialized to the object type.", Severity.Error);
                    }
                    return deserializedObject;
                }

                if (content == null)
                {
                    return default;
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
