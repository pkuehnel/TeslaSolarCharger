using TeslaSolarCharger.Server.Dtos.TscBackend;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ITeslaFleetApiService
{
    Task AddNewTokenAsync(DtoTeslaTscDeliveryToken token);
    Task<DtoValue<FleetApiTokenState>> GetFleetApiTokenState();
    Task RefreshTokenAsync();
    Task OpenChargePortDoor(int carId);
    Task<DtoValue<bool>> TestFleetApiAccess(int carId);
    DtoValue<bool> IsFleetApiEnabled();
    DtoValue<bool> IsFleetApiProxyEnabled();
}
