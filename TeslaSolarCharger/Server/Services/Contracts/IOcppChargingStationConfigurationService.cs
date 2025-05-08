using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Shared.Dtos.ChargingStation;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IOcppChargingStationConfigurationService
{
    Task AddChargingStationIfNotExisting(string chargepointId, CancellationToken httpContextRequestAborted);
    Task<List<DtoChargingStation>> GetChargingStations();
    Task UpdateChargingStation(DtoChargingStation dtoChargingStation);
}
