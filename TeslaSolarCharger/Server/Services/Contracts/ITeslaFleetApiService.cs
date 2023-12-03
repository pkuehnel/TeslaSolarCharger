using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.TeslaFleetApi;
using TeslaSolarCharger.Server.Dtos.TscBackend;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ITeslaFleetApiService
{
    Task AddNewTokenAsync(DtoTeslaTscDeliveryToken token);
    Task<DtoValue<FleetApiTokenState>> GetFleetApiTokenState();
    Task RefreshTokenAsync();
}
