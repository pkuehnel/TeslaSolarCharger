using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;
using TeslaSolarCharger.Shared.SignalRClients;

namespace TeslaSolarCharger.Server.SignalR.Notifiers.Contracts;

public interface IAppStateNotifier
{
    Task NotifyStateUpdateAsync(StateUpdateDto update);
    Task NotifyAllClientsAsync(StateUpdateDto update);
}
