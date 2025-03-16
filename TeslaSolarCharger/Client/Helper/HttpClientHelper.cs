using Microsoft.AspNetCore.Mvc;
using MudBlazor;
using Newtonsoft.Json;
using System.Net.Http.Json;
using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Shared.Dtos;

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

    public async Task<Result<T>> SendGetRequestAsync<T>(string url)
    {
        return await SendRequestCoreAsync<T>(HttpMethod.Get, url, null);
    }

    public async Task<Result<object>> SendGetRequestAsync(string url)
    {
        return await SendRequestCoreAsync<object>(HttpMethod.Get, url, null);
    }

    public async Task<Result<T>> SendPostRequestAsync<T>(string url, object? content)
    {
        return await SendRequestCoreAsync<T>(HttpMethod.Post, url, content);
    }

    public async Task<Result<object>> SendPostRequestAsync(string url, object? content)
    {
        return await SendRequestCoreAsync<object>(HttpMethod.Post, url, content);
    }

    private async Task<T?> SendRequestWithSnackbarInternalAsync<T>(
        HttpMethod method,
        string url,
        object? content)
    {
        try
        {
            // Call the same core method
            var result = await SendRequestCoreAsync<T>(method, url, content);

            if (result.HasError)
            {
                // Show error in Snackbar
                snackbar.Add(result.ErrorMessage ?? "EmptyErrorMessage", Severity.Error);
                return default;
            }

            // Return the deserialized data
            return result.Data;
        }
        catch (Exception ex)
        {
            // If you need special catch logic that includes a Snackbar, do it here.
            var message = $"{url}: Unexpected error: {ex.Message}";
            snackbar.Add(message, Severity.Error, config =>
            {
                config.Action = "Details";
                config.ActionColor = Color.Primary;
                config.OnClick = snackbar1 => dialogHelper.ShowTextDialog(
                    "Error Details",
                    $"Unexpected error while calling {url}: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
                );
            });

            return default;
        }
    }

    private async Task<Result<T>> SendRequestCoreAsync<T>(
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
                return new Result<T>(
                    default,
                    $"Unsupported HTTP method: {method}",
                    null
                );
            }

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();

                if (typeof(T) != typeof(object))
                {
                    var deserializedObject = JsonConvert.DeserializeObject<T>(responseContent);

                    if (deserializedObject == null)
                    {
                        return new Result<T>(
                            default,
                            $"{url}: Could not deserialize response to {typeof(T).Name}.",
                            null
                        );
                    }

                    return new Result<T>(deserializedObject, null, null);
                }
                else
                {
                    // If T=object, we don't do any deserialization
                    return new Result<T>(
                        default,
                        null,
                        null
                    );
                }
            }
            else
            {
                var resultString = await response.Content.ReadAsStringAsync();
                var problemDetails = JsonConvert.DeserializeObject<ValidationProblemDetails>(resultString);
                var message = problemDetails != null
                    ? $"Error: {problemDetails.Detail}"
                    : "An error occurred on the server.";

                return new Result<T>(default, message, problemDetails);
            }
        }
        catch (HttpRequestException ex)
        {
            // Network-level error
            var message = $"{url}: Network error: {ex.Message}";
            return new Result<T>(default, message, null);
        }
        catch (Exception ex)
        {
            // Any other unexpected error
            var message = $"{url}: Unexpected error: {ex.Message}";
            return new Result<T>(default, message, null);
        }
    }
}
