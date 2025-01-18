using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Client.Services.Contracts;

public interface ICloudConnectionCheckService
{
    Task<TokenState> GetBackendTokenState(bool useCache);
    Task<string?> GetBackendTokenUserName();
    Task<TokenState> GetFleetApiTokenState(bool useCache);
    Task<string?> GetTeslaLoginUrl(string locale, string baseUrl);
}
