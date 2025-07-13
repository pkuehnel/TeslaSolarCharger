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
    Task<List<DtoLoadPointWithCurrentChargingValues>> GetLoadPointsWithChargingDetails();

    Task<HashSet<DtoLoadpointCombination>> GetCarConnectorMatches(IEnumerable<int> carIds,
        IEnumerable<int> connectorIds, bool updateSettingsMatches);

    void UpdateChargingConnectorCar(int chargingConnectorId, int? carId);
}
