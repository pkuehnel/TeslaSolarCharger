using Newtonsoft.Json;
using TeslaSolarCharger.BleApi.Dtos;
using TeslaSolarCharger.BleApi.InMemoryValues.Contracts;
using TeslaSolarCharger.BleApi.Services.Contracts;

namespace TeslaSolarCharger.BleApi.Services;

public class StartupService(ILogger<StartupService> logger,
    ISettings settings,
    IConfiguration configuration,
    TimeProvider timeProvider,
    IHttpClientFactory httpClientFactory,
    IPairingService pairingService) : IStartupService
{
    public async Task EnsureKeyPair()
    {
        try
        {
            await pairingService.EnsureKeyPair().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            //The container must still start: everything but the worker keeps working, and pairing retries.
            logger.LogError(ex, "Could not create the key pair on startup");
        }
    }

    public async Task UpdateRequestsAllowed()
    {
        settings.BleRequestAllowed = true;
        return;
        var guid = Guid.NewGuid().ToString();
        var httpClient = httpClientFactory.CreateClient();
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var baseUrl = configuration.GetValue<string>("BackendApiBaseUrl");
        var url = baseUrl + $"Ble/AllowUnlimitedBleRequests?installationId={guid}";
        try
        {
            var response = await httpClient.GetAsync(url, cancellationTokenSource.Token).ConfigureAwait(false);
            var responseString = await response.Content.ReadAsStringAsync(cancellationTokenSource.Token).ConfigureAwait(false);
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