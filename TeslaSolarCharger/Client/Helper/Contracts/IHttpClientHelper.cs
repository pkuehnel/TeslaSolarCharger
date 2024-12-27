namespace TeslaSolarCharger.Client.Helper.Contracts;

public interface IHttpClientHelper
{
    Task<T?> SendGetRequestWithSnackbarAsync<T>(string url);
    Task SendGetRequestWithSnackbarAsync(string url);
    Task<T?> SendPostRequestWithSnackbarAsync<T>(string url, object? content);
    Task SendPostRequestWithSnackbarAsync(string url, object? content);
}
