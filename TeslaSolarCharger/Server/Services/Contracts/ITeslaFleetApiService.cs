using LanguageExt;
using TeslaSolarCharger.Server.Dtos.Solar4CarBackend;
using TeslaSolarCharger.Server.Dtos.TeslaFleetApi;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Car;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ITeslaFleetApiService
{
    Task<DtoValue<TokenState>> GetFleetApiTokenState(bool useCache);
    Task<DtoGenericTeslaResponse<DtoVehicleWakeUpResult>?> WakeUpCar(int carId, bool isFleetApiTest);
    Task<DtoGenericTeslaResponse<DtoVehicleCommandResult>?> SetChargingAmps(int carId, int amps);
    Task<DtoGenericTeslaResponse<DtoVehicleResult>?> GetVehicleOnlineState(int carId);
    Task<DtoValue<bool>> TestFleetApiAccess(int carId);
    Task<DtoValue<bool>> IsFleetApiProxyEnabled(string vin);
    Task RefreshCarData();
    Task RefreshFleetApiRequestsAreAllowed();

    void ResetApiRequestCounters();
    Task<Fin<List<DtoTesla>>> GetAllCarsFromAccount();
    Task RefreshFleetApiTokenIfRequired();
    Task<DtoBackendApiTeslaResponse> GetAllProductsFromTeslaAccount();
    Task<DtoBackendApiTeslaResponse> GetEnergyLiveStatus(string energySiteId);
    Task<DtoBackendApiTeslaResponse> SetEnergySiteBackupReserve(string energySiteId, int backupReservePercent);

    Task<TeslaCarFleetApiState?> GetFleetApiState(int carId);
    Task RefreshVehicleOnlineState(DtoCar car);
    Task<Dictionary<long, string?>> GetEnergySites();
}
