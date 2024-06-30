using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IBackendApiService
{
    Task<DtoValue<string>> StartTeslaOAuth(string locale, string baseUrl);
    Task PostInstallationInformation(string reason);
    Task PostErrorInformation(string source, string methodName, string message, string? stackTrace = null);
    Task<string?> GetCurrentVersion();
    Task PostTeslaApiCallStatistics();
}
