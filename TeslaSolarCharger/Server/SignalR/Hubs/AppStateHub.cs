using Microsoft.AspNetCore.SignalR;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Helper.Contracts;
using TeslaSolarCharger.Shared.SignalRClients;

namespace TeslaSolarCharger.Server.SignalR.Hubs;

public class AppStateHub : Hub<IAppStateClient>
{
    private readonly IStateSnapshotService _stateSnapshotService;
    private readonly ILogger<AppStateHub> _logger;
    private readonly IEntityKeyGenerationHelper _entityKeyGenerationHelper;

    public AppStateHub(IStateSnapshotService stateSnapshotService, ILogger<AppStateHub> logger, IEntityKeyGenerationHelper entityKeyGenerationHelper)
    {
        _stateSnapshotService = stateSnapshotService;
        _logger = logger;
        _entityKeyGenerationHelper = entityKeyGenerationHelper;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public async Task SubscribeToDataType(string dataType)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, dataType);
    }

    public async Task GetStateFor(string dataType, string entityId)
    {
        // Send current state for this specific data type
        var currentState = await _stateSnapshotService.GetCurrentStateAsync(dataType, entityId);
        if (!string.IsNullOrEmpty(currentState))
        {
            var key = _entityKeyGenerationHelper.GetDataKey(dataType, entityId);
            await Clients.Caller.ReceiveInitialState(key, currentState);
        }
    }

    public async Task UnsubscribeFromDataType(string dataType)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, dataType);
    }
}
