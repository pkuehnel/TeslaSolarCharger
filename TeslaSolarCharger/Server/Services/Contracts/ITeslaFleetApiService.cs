using TeslaSolarCharger.Server.Dtos.TscBackend;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ITeslaFleetApiService
{
    Task AddNewTokenAsync(DtoTeslaTscDeliveryToken token);
    Task<DtoValue<FleetApiTokenState>> GetFleetApiTokenState();
    Task GetNewTokenFromBackend();
    Task OpenChargePortDoor(int carId);
    Task<DtoValue<bool>> TestFleetApiAccess(int carId);
    DtoValue<bool> IsFleetApiEnabled();
    Task<DtoValue<bool>> IsFleetApiProxyEnabled(string vin);
    Task RefreshCarData();
    Task RefreshTokensIfAllowedAndNeeded();
    Task RefreshFleetApiRequestsAreAllowed();

    void ResetApiRequestCounters();
}
