using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ITeslaFleetApiTokenHelper
{
    Task<FleetApiTokenState> GetFleetApiTokenState();
    Task<DateTime?> GetTokenRequestedDate();
}
