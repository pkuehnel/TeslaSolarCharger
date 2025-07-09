namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IStateSnapshotService
{
    Task<Dictionary<string, string>> GetAllCurrentStatesAsync();
    Task<string> GetCurrentStateAsync(string dataType);
}
