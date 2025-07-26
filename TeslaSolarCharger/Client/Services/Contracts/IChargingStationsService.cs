using TeslaSolarCharger.Shared.Dtos.ChargingStation;

namespace TeslaSolarCharger.Client.Services.Contracts;

public interface IChargingStationsService
{
    Task<List<DtoChargingStation>?> GetChargingStations();
    Task<List<DtoChargingStationConnector>?> GetChargingStationConnectors(int chargingStationId);
}
