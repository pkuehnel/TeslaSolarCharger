using LanguageExt;
using TeslaSolarCharger.Server.Dtos.Solar4CarBackend;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Car;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ITeslaFleetApiService
{
    Task<DtoValue<TokenState>> GetFleetApiTokenState(bool useCache);
    Task<DtoValue<bool>> TestFleetApiAccess(int carId);
    Task<DtoValue<bool>> IsFleetApiProxyEnabled(string vin);
    Task RefreshCarData();
    Task RefreshFleetApiRequestsAreAllowed();

    void ResetApiRequestCounters();
    Task<Fin<List<DtoTesla>>> GetNewCarsInAccount();
    Task<Fin<List<DtoTesla>>> GetAllCarsFromAccount();
    Task RefreshFleetApiTokenIfNeeded();
    Task<DtoBackendApiTeslaResponse> GetAllProductsFromTeslaAccount();
    Task<DtoBackendApiTeslaResponse> GetEnergyLiveStatus(string energySiteId);

}
