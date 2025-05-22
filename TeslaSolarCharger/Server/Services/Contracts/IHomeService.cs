using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IHomeService
{
    Task<List<DtoLoadPointOverview>> GetPluggedInLoadPoints();
}
