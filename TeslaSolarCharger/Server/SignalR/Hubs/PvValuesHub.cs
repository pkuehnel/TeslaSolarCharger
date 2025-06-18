using Microsoft.AspNetCore.SignalR;
using TeslaSolarCharger.Shared.SignalRClients;

namespace TeslaSolarCharger.Server.SignalR.Hubs;

public class PvValuesHub : Hub<IPvValuesClient>
{
}
