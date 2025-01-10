using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Client.Helper.Contracts;

public interface IHttpClientHelper
{
    Task<T?> SendGetRequestWithSnackbarAsync<T>(string url);
    Task SendGetRequestWithSnackbarAsync(string url);
    Task<T?> SendPostRequestWithSnackbarAsync<T>(string url, object? content);
    Task SendPostRequestWithSnackbarAsync(string url, object? content);
    Task<Result<T>> SendGetRequestAsync<T>(string url);
    Task<Result<object>> SendGetRequestAsync(string url);
    Task<Result<T>> SendPostRequestAsync<T>(string url, object? content);
    Task<Result<object>> SendPostRequestAsync(string url, object? content);
}
