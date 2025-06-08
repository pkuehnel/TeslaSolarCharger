using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ILoadPointManagementService
{
    Task<List<DtoLoadPointOverview>> GetLoadPointsToManage();

    /// <summary>
    /// Get all load points with their charging power and voltage details. Lightweight method without database calls.
    /// </summary>
    /// <returns></returns>
    List<DtoLoadPointWithCurrentChargingValues> GetLoadPointsWithChargingDetails();

    HashSet<(int? CarId, int? ConnectorId)> GetCarConnectorMatches(
        IEnumerable<int> carIds,
        IEnumerable<int> connectorIds);
}
