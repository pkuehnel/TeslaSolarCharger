using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ITeslaFleetApiTokenHelper
{
    Task<TokenState> GetFleetApiTokenState();
    Task<TokenState> GetBackendTokenState();
}
