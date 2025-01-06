using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Client.Services.Contracts;

public interface IBackendApiTokenCheckService
{
    Task<TokenState> GetTokenState(bool useCache);
}
