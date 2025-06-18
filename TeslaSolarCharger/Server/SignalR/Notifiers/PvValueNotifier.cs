using Microsoft.AspNetCore.SignalR;
using TeslaSolarCharger.Server.SignalR.Hubs;
using TeslaSolarCharger.Server.SignalR.Notifiers.Contracts;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;
using TeslaSolarCharger.Shared.SignalRClients;

namespace TeslaSolarCharger.Server.SignalR.Notifiers;

public class PvValueNotifier : IPvValueNotifier
{
    private readonly IHubContext<PvValuesHub, IPvValuesClient> _hubContext;

    public PvValueNotifier(
        IHubContext<PvValuesHub, IPvValuesClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyNewValuesAsync(DtoPvValues newValues)
    {
        await _hubContext.Clients.All.ReceivePvValues(newValues)
            .ConfigureAwait(false);
    }
}
