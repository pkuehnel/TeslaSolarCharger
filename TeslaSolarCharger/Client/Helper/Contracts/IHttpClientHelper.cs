using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Client.Helper.Contracts;

public interface IHttpClientHelper
{
    Task<T?> SendGetRequestWithSnackbarAsync<T>(string url, CancellationToken cancellationToken = new());
    Task SendGetRequestWithSnackbarAsync(string url, CancellationToken cancellationToken = new());
    Task<T?> SendPostRequestWithSnackbarAsync<T>(string url, object? content, CancellationToken cancellationToken = new());
    Task SendPostRequestWithSnackbarAsync(string url, object? content, CancellationToken cancellationToken = new());
    Task<Result<T>> SendGetRequestAsync<T>(string url, CancellationToken cancellationToken = new());
    Task<Result<object>> SendGetRequestAsync(string url, CancellationToken cancellationToken = new());
    Task<Result<T>> SendPostRequestAsync<T>(string url, object? content, CancellationToken cancellationToken = new());
    Task<Result<object>> SendPostRequestAsync(string url, object? content, CancellationToken cancellationToken = new());
    Task<T?> SendDeleteRequestWithSnackbarAsync<T>(string url, CancellationToken cancellationToken = new());
    Task SendDeleteRequestWithSnackbarAsync(string url, CancellationToken cancellationToken = new());
    Task<Result<T>> SendDeleteRequestAsync<T>(string url, CancellationToken cancellationToken = new());
    Task<Result<object>> SendDeleteRequestAsync(string url, CancellationToken cancellationToken = new());
}
