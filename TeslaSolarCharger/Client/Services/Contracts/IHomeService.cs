using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Client.Services.Contracts;

public interface IHomeService
{
    Task<List<DtoLoadPointOverview>?> GetPluggedInLoadPoints();
}
