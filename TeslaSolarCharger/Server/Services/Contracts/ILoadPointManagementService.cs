using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ILoadPointManagementService
{
    Task<List<DtoLoadpoint>> GetPluggedInLoadPoints();
}
