using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ILoadPointManagementService
{
    Task<List<DtoLoadpoint>> GetPluggedInLoadPoints();
    Task<HashSet<(int? carId, int? connectorId)>> GetLoadPointsToManage();
}
