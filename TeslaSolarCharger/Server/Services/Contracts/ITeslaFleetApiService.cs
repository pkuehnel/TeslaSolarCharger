using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.TeslaFleetApi;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ITeslaFleetApiService
{
    public Task AddNewTokenAsync(DtoTeslaFleetApiRefreshToken token, TeslaFleetApiRegion region);
    Task<DtoValue<FleetApiTokenState>> GetFleetApiTokenState();
}
