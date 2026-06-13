using Newtonsoft.Json;
using TeslaSolarCharger.BleApi.Dtos;
using TeslaSolarCharger.BleApi.InMemoryValues.Contracts;
using TeslaSolarCharger.BleApi.Services.Contracts;

namespace TeslaSolarCharger.BleApi.Services;

public class StartupService(ILogger<StartupService> logger,
    ISettings settings,
    IConfiguration configuration,
    TimeProvider timeProvider) : IStartupService
{
    public async Task UpdateRequestsAllowed()
    {
        settings.BleRequestAllowed = true;
        return;
        var guid = Guid.NewGuid().ToString();
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(25);
        var baseUrl = configuration.GetValue<string>("BackendApiBaseUrl");
        var url = baseUrl + $"Ble/AllowUnlimitedBleRequests?installationId={guid}";
        try
        {
            var response = await httpClient.GetAsync(url).ConfigureAwait(false);
            var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                settings.BleRequestAllowed = false;
                return;
            }

            var responseValue = JsonConvert.DeserializeObject<DtoValue<bool>>(responseString);
            settings.BleRequestAllowed = responseValue?.Value == true;
            settings.LastBleAllowedRequest = timeProvider.GetUtcNow();
        }
        catch (Exception)
        {
            logger.LogError("Failed to check for unlimited requests allowed");
            settings.BleRequestAllowed = false;
        }
    }
}