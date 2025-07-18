using TeslaSolarCharger.Shared.SignalRClients;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IChangeTrackingService
{
    StateUpdateDto? DetectChanges<T>(string dataType, string? entityId, T currentState) where T : class;
    void ClearState(string dataType, string entityId);
}
