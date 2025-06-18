using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using TeslaSolarCharger.Client.Helper.Contracts;

namespace TeslaSolarCharger.Client.Helper;

/// <summary>
/// Manages a single SignalR HubConnection: configure, start, register handlers, invoke methods, dispose.
/// </summary>
public class SignalRConnector : ISignalRConnector
{
    private HubConnection? _hubConnection;
    private readonly NavigationManager _navigationManager;

    public SignalRConnector(NavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
    }

    /// <summary>
    /// Build or rebuild the HubConnection to the given hub endpoint.
    /// </summary>
    public void Configure(string hubEndpointUrl)
    {
        if (_hubConnection != default)
        {
            _ = _hubConnection.DisposeAsync();
        }

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_navigationManager.ToAbsoluteUri(hubEndpointUrl))
            .WithAutomaticReconnect()
            .Build();
    }

    /// <summary>
    /// Start the connection; throws on failure.
    /// </summary>
    public async Task StartAsync()
    {
        if (_hubConnection is null)
        {
            throw new InvalidOperationException("Connector has not been configured with a hub URL.");
        }

        await _hubConnection
            .StartAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Register a handler for a hub method.
    /// </summary>
    public void RegisterHandler<TMessage>(
        string methodName,
        Action<TMessage> handler)
    {
        if (_hubConnection is null)
        {
            throw new InvalidOperationException("Connector has not been configured.");
        }

        _hubConnection.On<TMessage>(
            methodName,
            handler);
    }

    /// <summary>
    /// Call a hub method on the server.
    /// </summary>
    public async Task InvokeAsync(
        string methodName,
        params object[] args)
    {
        if (_hubConnection is null)
        {
            throw new InvalidOperationException("Connector has not been configured.");
        }

        await _hubConnection
            .InvokeAsync(methodName, args)
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != default)
        {
            await _hubConnection
                .DisposeAsync()
                .ConfigureAwait(false);
        }
    }
}
