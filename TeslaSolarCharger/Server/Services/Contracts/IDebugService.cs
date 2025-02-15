using TeslaSolarCharger.Shared.Dtos.Support;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IDebugService
{
    Task<Dictionary<int, DtoDebugCar>> GetCars();
    byte[] GetLogBytes();
    void SetLogLevel(string level);
    void SetLogCapacity(int capacity);
    string GetLogLevel();
    int GetLogCapacity();
}
