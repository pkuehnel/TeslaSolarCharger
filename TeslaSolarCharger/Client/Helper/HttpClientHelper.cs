using Microsoft.AspNetCore.Mvc;
using MudBlazor;
using Newtonsoft.Json;
using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Client.Helper.Contracts;

namespace TeslaSolarCharger.Client.Helper;

public class HttpClientHelper(HttpClient httpClient, ISnackbar snackbar, IDialogHelper dialogHelper) : IHttpClientHelper
{
    public async Task<T?> SendGetRequestWithSnackbarAsync<T>(string url, CancellationToken cancellationToken)
    {
        return await SendRequestWithSnackbarInternalAsync<T>(HttpMethod.Get, url, null, cancellationToken);
    }
    public async Task SendGetRequestWithSnackbarAsync(string url, CancellationToken cancellationToken)
    {
        await SendRequestWithSnackbarInternalAsync<object>(HttpMethod.Get, url, null, cancellationToken);
    }
    public async Task<Result<T>> SendGetRequestAsync<T>(string url, CancellationToken cancellationToken)
    {
        return await SendRequestCoreAsync<T>(HttpMethod.Get, url, null, cancellationToken);
    }
    public async Task<Result<object>> SendGetRequestAsync(string url, CancellationToken cancellationToken)
    {
        return await SendRequestCoreAsync<object>(HttpMethod.Get, url, null, cancellationToken);
    }

    public async Task<T?> SendPostRequestWithSnackbarAsync<T>(string url, object? content, CancellationToken cancellationToken)
    {
        return await SendRequestWithSnackbarInternalAsync<T>(HttpMethod.Post, url, content, cancellationToken);
    }
    public async Task SendPostRequestWithSnackbarAsync(string url, object? content, CancellationToken cancellationToken)
    {
        await SendRequestWithSnackbarInternalAsync<object>(HttpMethod.Post, url, content, cancellationToken);
    }
    public async Task<Result<T>> SendPostRequestAsync<T>(string url, object? content, CancellationToken cancellationToken)
    {
        return await SendRequestCoreAsync<T>(HttpMethod.Post, url, content, cancellationToken);
    }
    public async Task<Result<object>> SendPostRequestAsync(string url, object? content, CancellationToken cancellationToken)
    {
        return await SendRequestCoreAsync<object>(HttpMethod.Post, url, content, cancellationToken);
    }

    public async Task<T?> SendDeleteRequestWithSnackbarAsync<T>(string url, CancellationToken cancellationToken)
    {
        return await SendRequestWithSnackbarInternalAsync<T>(HttpMethod.Delete, url, null, cancellationToken);
    }
    public async Task SendDeleteRequestWithSnackbarAsync(string url, CancellationToken cancellationToken)
    {
        await SendRequestWithSnackbarInternalAsync<object>(HttpMethod.Delete, url, null, cancellationToken);
    }
    public async Task<Result<T>> SendDeleteRequestAsync<T>(string url, CancellationToken cancellationToken)
    {
        return await SendRequestCoreAsync<T>(HttpMethod.Delete, url, null, cancellationToken);
    }
    public async Task<Result<object>> SendDeleteRequestAsync(string url, CancellationToken cancellationToken)
    {
        return await SendRequestCoreAsync<object>(HttpMethod.Delete, url, null, cancellationToken);
    }

    private async Task<T?> SendRequestWithSnackbarInternalAsync<T>(HttpMethod method,
        string url,
        object? content, CancellationToken cancellationToken)
    {
        try
        {
            var result = await SendRequestCoreAsync<T>(method, url, content, cancellationToken);
            if (result.HasError)
            {
                snackbar.Add(result.ErrorMessage ?? "EmptyErrorMessage", Severity.Error);
                return default;
            }

            return result.Data;
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var message = $"{url}: Unexpected error: {ex.Message}";
            snackbar.Add(message, Severity.Error, config =>
            {
                config.Action = "Details";
                config.ActionColor = Color.Primary;
                config.OnClick = snackbar1 => dialogHelper.ShowTextDialog(
                    "Error Details",
                    $"Unexpected error while calling {url}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            });
            return default;
        }
    }

    private async Task<Result<T>> SendRequestCoreAsync<T>(
        HttpMethod method,
        string url,
        object? content,
        CancellationToken cancellationToken)
    {
        try
        {
            HttpResponseMessage response;

            if (method == HttpMethod.Get)
            {
                response = await httpClient.GetAsync(url, cancellationToken);
            }
            else if (method == HttpMethod.Post)
            {
                var jsonContent = new StringContent(
                    JsonConvert.SerializeObject(content),
                    System.Text.Encoding.UTF8,
                    "application/json");
                response = await httpClient.PostAsync(url, jsonContent, cancellationToken);
            }
            else if (method == HttpMethod.Delete)
            {
                response = await httpClient.DeleteAsync(url, cancellationToken);
            }
            else
            {
                return new Result<T>(default, $"Unsupported HTTP method: {method}", null);
            }

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                if (typeof(T) != typeof(object))
                {
                    var deserializedObject = JsonConvert.DeserializeObject<T>(responseContent);
                    if (deserializedObject == null)
                    {
                        return new Result<T>(default,
                            $"{url}: Could not deserialize response to {typeof(T).Name}.", null);
                    }
                    return new Result<T>(deserializedObject, null, null);
                }
                else
                {
                    return new Result<T>(default, null, null);
                }
            }
            else
            {
                var resultString = await response.Content.ReadAsStringAsync(cancellationToken);
                var problemDetails = JsonConvert.DeserializeObject<ValidationProblemDetails>(resultString);
                var message = problemDetails != null
                    ? $"Error: {problemDetails.Detail}"
                    : "An error occurred on the server.";
                return new Result<T>(default, message, problemDetails);
            }
        }
        catch(TaskCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            var message = $"{url}: Network error: {ex.Message}";
            return new Result<T>(default, message, null);
        }
        catch (Exception ex)
        {
            var message = $"{url}: Unexpected error: {ex.Message}";
            return new Result<T>(default, message, null);
        }
    }
}
