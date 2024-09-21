using Microsoft.AspNetCore.Mvc;
using MudBlazor;
using System.Net.Http.Json;

namespace TeslaSolarCharger.Client.Extensions;

public static class HttpClientExtensions
{
    public static async Task SendGetRequestWithSnackbarAsync(
        this HttpClient httpClient,
        string url,
        ISnackbar snackbar)
    {
        try
        {
            var response = await httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                snackbar.Add(content, Severity.Success);
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
    }
}
