using Microsoft.AspNetCore.SignalR;
using TeslaSolarCharger.Server.SignalR.Hubs;
using TeslaSolarCharger.Server.SignalR.Notifiers.Contracts;
using TeslaSolarCharger.Shared.SignalRClients;

namespace TeslaSolarCharger.Server.SignalR.Notifiers;

public class AppStateNotifier : IAppStateNotifier
{
    private readonly IHubContext<AppStateHub, IAppStateClient> _hubContext;
    private readonly ILogger<AppStateNotifier> _logger;

    public AppStateNotifier(
        IHubContext<AppStateHub, IAppStateClient> hubContext,
        ILogger<AppStateNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyStateUpdateAsync(StateUpdateDto update)
    {
        _logger.LogDebug("Notifying state update for {DataType}", update.DataType);

        // Send to all clients subscribed to this data type
        await _hubContext.Clients.Group(update.DataType)
            .ReceiveStateUpdate(update);
    }

    public async Task NotifyAllClientsAsync(StateUpdateDto update)
    {
        await _hubContext.Clients.All.ReceiveStateUpdate(update);
    }
}
