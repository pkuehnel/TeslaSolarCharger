using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IBackendApiService
{
    Task<DtoValue<string>> StartTeslaOAuth(string locale, string baseUrl);
    Task PostInstallationInformation(string reason);
    Task<string?> GetCurrentVersion();
    Task GetNewBackendNotifications();
    Task GetToken(DtoBackendLogin login);
    Task RefreshBackendTokenIfNeeded();
    Task<Dtos.Result<T>> SendRequestToBackend<T>(HttpMethod httpMethod, string? accessToken, string requestUrlPart, object? content);
    Task<bool> IsBaseAppLicensed(bool useCache);
    Task<bool> IsFleetApiLicensed(string vin, bool useCache);
}
