using Microsoft.AspNetCore.SignalR;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.SignalRClients;

namespace TeslaSolarCharger.Server.SignalR.Hubs;

public class AppStateHub : Hub<IAppStateClient>
{
    private readonly IStateSnapshotService _stateSnapshotService;
    private readonly ILogger<AppStateHub> _logger;

    public AppStateHub(IStateSnapshotService stateSnapshotService, ILogger<AppStateHub> logger)
    {
        _stateSnapshotService = stateSnapshotService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);

        // Send initial state for all data types
        var initialStates = await _stateSnapshotService.GetAllCurrentStatesAsync();
        foreach (var state in initialStates)
        {
            await Clients.Caller.ReceiveInitialState(state.Key, state.Value);
        }

        await base.OnConnectedAsync();
    }

    public async Task SubscribeToDataType(string dataType)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, dataType);

        // Send current state for this specific data type
        var currentState = await _stateSnapshotService.GetCurrentStateAsync(dataType);
        if (!string.IsNullOrEmpty(currentState))
        {
            await Clients.Caller.ReceiveInitialState(dataType, currentState);
        }
    }

    public async Task UnsubscribeFromDataType(string dataType)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, dataType);
    }
}
