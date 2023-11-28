using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.TeslaFleetApi;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ITeslaFleetApiService
{
    public Task AddNewTokenAsync(DtoTeslaFleetApiRefreshToken token, TeslaFleetApiRegion region);
}
