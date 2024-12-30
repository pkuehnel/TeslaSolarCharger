using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ITokenHelper
{
    Task<TokenState> GetFleetApiTokenState(bool useCache);
    Task<TokenState> GetBackendTokenState(bool useCache);
    Task<DateTimeOffset?> GetFleetApiTokenExpirationDate();
    Task<DateTimeOffset?> GetBackendTokenExpirationDate();
}
