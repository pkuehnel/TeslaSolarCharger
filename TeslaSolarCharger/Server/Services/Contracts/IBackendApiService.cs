using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IBackendApiService
{
    Task<DtoValue<string>> StartTeslaOAuth(string locale, string baseUrl);
    Task PostInstallationInformation(string reason);
    Task PostErrorInformation(string source, string methodName, string message, string issueKey, string? vin, string? stackTrace);
    Task<string?> GetCurrentVersion();
    Task GetNewBackendNotifications();
    Task<DtoValue<bool>> HasValidBackendToken();
    Task GetToken(DtoBackendLogin login);
    Task RefreshBackendTokenIfNeeded();
}
