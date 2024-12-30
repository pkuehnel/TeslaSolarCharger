using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Client.Services.Contracts;

public interface IFleetApiTokenCheckService
{
    Task<TokenState> HasValidBackendToken();
}
