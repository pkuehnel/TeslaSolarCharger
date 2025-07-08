namespace TeslaSolarCharger.Client.Helper.Contracts;

public interface ISignalRConnector : IAsyncDisposable
{
    /// <summary>
    /// Build or rebuild the HubConnection to the given hub endpoint.
    /// </summary>
    void Configure(string hubEndpointUrl);

    /// <summary>
    /// Start the connection; throws on failure.
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Register a handler for a hub method.
    /// </summary>
    void RegisterHandler<TMessage>(
        string methodName,
        Action<TMessage> handler);

    /// <summary>
    /// Call a hub method on the server.
    /// </summary>
    Task InvokeAsync(
        string methodName,
        params object[] args);
}
