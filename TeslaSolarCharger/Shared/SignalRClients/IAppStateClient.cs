using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;

namespace TeslaSolarCharger.Shared.SignalRClients;

public interface IAppStateClient
{
    Task ReceiveStateUpdate(StateUpdateDto update);
    Task ReceiveInitialState(string dataType, string jsonData);
}
