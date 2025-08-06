namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IStateSnapshotService
{
    Task<Dictionary<string, object?>> GetAllCurrentStatesAsync();
    Task<object?> GetCurrentStateAsync(string dataType, string entityId);
}
