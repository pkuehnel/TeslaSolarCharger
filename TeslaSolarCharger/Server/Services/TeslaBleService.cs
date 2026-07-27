using Newtonsoft.Json;
using System.Net;
using System.Web;
using TeslaSolarCharger.Server.Dtos.Ble;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Ble;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources;

namespace TeslaSolarCharger.Server.Services;

public class TeslaBleService(ILogger<TeslaBleService> logger,
    ISettings settings,
    IErrorHandlingService errorHandlingService,
    IIssueKeys issueKeys,
    IHttpClientFactory httpClientFactory) : IBleService
{
    private static readonly TimeSpan PairKeyTimeout = TimeSpan.FromSeconds(100);
    private static readonly TimeSpan VersionCheckTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DownloadLogsTimeout = TimeSpan.FromSeconds(30);
    //Slightly below the BLE containers own command timeout so the container can answer first.
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(29);

    public async Task<DtoBleCommandResult> StartCharging(string vin)
    {
        logger.LogTrace("{method}({vin})", nameof(StartCharging), vin);
        var request = new DtoBleRequest
        {
            Vin = vin,
            CommandName = "charging-start",
        };
        var result = await SendCommandToBle(request).ConfigureAwait(false);
        return result;
    }

    public async Task<DtoBleCommandResult> WakeUpCar(string vin)
    {
        var request = new DtoBleRequest
        {
            Vin = vin,
            CommandName = "wake",
            Domain = "vcsec",
        };
        var result = await SendCommandToBle(request).ConfigureAwait(false);
        return result;
    }

    public async Task<DtoBleCommandResult> GetChargeState(string vin)
    {
        //Not tested, should contain a json with the charge state. Other options would be climate, drive, closures, charge-schedule, precondition-schedule, tire-pressue, media, media-detail, software-update
        logger.LogTrace("{method}({vin})", nameof(GetChargeState), vin);
        var request = new DtoBleRequest
        {
            Vin = vin,
            CommandName = "state",
            Parameters = ["charge"],
        };
        var result = await SendCommandToBle(request).ConfigureAwait(false);
        return result;
    }

    public async Task<DtoBleCommandResult> GetDriveState(string vin)
    {
        //Not tested
        logger.LogTrace("{method}({vin})", nameof(GetDriveState), vin);
        var request = new DtoBleRequest
        {
            Vin = vin,
            CommandName = "state",
            Parameters = ["drive"],
        };
        var result = await SendCommandToBle(request).ConfigureAwait(false);
        return result;
    }

    public async Task<DtoBleCommandResult> GetBodyControllerState(string vin)
    {
        logger.LogTrace("{method}({vin})", nameof(GetBodyControllerState), vin);
        var request = new DtoBleRequest
        {
            Vin = vin,
            CommandName = "body-controller-state",
        };
        var result = await SendCommandToBle(request).ConfigureAwait(false);
        return result;
    }

    public async Task<DtoBleCommandResult> GetBeaconScanResult(string vin)
    {
        logger.LogTrace("{method}({vin})", nameof(GetBeaconScanResult), vin);
        var bleBaseUrl = GetBleBaseUrl(vin);
        if (string.IsNullOrWhiteSpace(bleBaseUrl))
        {
            return new DtoBleCommandResult()
            {
                Success = false,
                ResultMessage = "BLE Base URL is not set. Set a BLE URL in your base configuration.",
                ErrorType = ErrorType.TscConfiguration,
            };
        }
        var queryString = HttpUtility.ParseQueryString(string.Empty);
        queryString.Add("vin", vin);
        var url = $"{bleBaseUrl}Command/BeaconScan?{queryString}";
        logger.LogTrace("Ble Url: {bleUrl}", url);
        var client = CreateBleClient();
        using var cancellationTokenSource = new CancellationTokenSource(CommandTimeout);
        try
        {
            var response = await client.GetAsync(url, cancellationTokenSource.Token).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                //Old BLE containers answer with 404 as they do not know the endpoint yet: the caller falls back to
                //the body controller state based presence detection, the version mismatch is surfaced separately.
                logger.LogError("Failed to get beacon scan result. StatusCode: {statusCode} {responseContent}", response.StatusCode, responseContent);
                throw new InvalidOperationException();
            }
            var commandResult = JsonConvert.DeserializeObject<DtoBleCommandResult>(responseContent) ?? throw new InvalidDataException($"Could not parse {responseContent} to {nameof(DtoBleCommandResult)}");
            return commandResult;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get beacon scan result.");
            return new DtoBleCommandResult()
            {
                ResultMessage = GetErrorMessage(ex, CommandTimeout),
                Success = false,
                ErrorType = ErrorType.Unknown,
            };
        }
    }

    public async Task<DtoBleCommandResult> StopCharging(string vin)
    {
        var request = new DtoBleRequest
        {
            Vin = vin,
            CommandName = "charging-stop",
        };
        var result = await SendCommandToBle(request).ConfigureAwait(false);
        return result;
    }

    public async Task<DtoBleCommandResult> SetAmp(string vin, int amps)
    {
        logger.LogTrace("{method}({vin}, {amps})", nameof(SetAmp), vin, amps);
        var car = settings.Cars.First(c => c.Vin == vin);
        var initialRequestedCurrent = car.ChargerRequestedCurrent.Value;
        var request = new DtoBleRequest
        {
            Vin = vin,
            CommandName = "charging-set-amps",
            Parameters = [amps.ToString()],
        };
        var result = await SendCommandToBle(request).ConfigureAwait(false);

        // Double send if over or under 5 amps as Tesla does not change immedediatly
        if (initialRequestedCurrent >= 5 && amps < 5 || initialRequestedCurrent < 5 && amps >= 5)
        {
            logger.LogDebug("Send charging amp command again");
            await Task.Delay(5000).ConfigureAwait(false);
            result = await SendCommandToBle(request).ConfigureAwait(false);
        }

        return result;
    }

    public async Task<DtoBleCommandResult> FlashLights(string vin)
    {
        var request = new DtoBleRequest
        {
            Vin = vin,
            CommandName = "flash-lights",
        };
        var result = await SendCommandToBle(request).ConfigureAwait(false);
        return result;
    }

    public async Task<DtoBleCommandResult> PairKey(string vin, string apiRole)
    {
        logger.LogTrace("{method}({vin}, {apiRole})", nameof(PairKey), vin, apiRole);
        var bleBaseUrl = GetBleBaseUrl(vin);
        if (string.IsNullOrWhiteSpace(bleBaseUrl))
        {
            return new()
            {
                ResultMessage = "BLE Base URL is not set. Set a BLE URL in your base configuration.",
                ErrorType = ErrorType.TscConfiguration,
                Success = false,
            };
        }
        
        bleBaseUrl += "Pairing/PairCar";
        var queryString = HttpUtility.ParseQueryString(string.Empty);
        queryString.Add("vin", vin);
        queryString.Add("apiRole", apiRole);
        var url = $"{bleBaseUrl}?{queryString}";
        logger.LogTrace("Ble Url: {bleUrl}", url);
        var client = CreateBleClient();
        using var cancellationTokenSource = new CancellationTokenSource(PairKeyTimeout);
        try
        {
            var response = await client.GetAsync(url, cancellationTokenSource.Token).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new()
                {
                    ResultMessage = responseContent,
                    ErrorType = ErrorType.TscConfiguration,
                    Success = false,
                };
            }
            var commandResult = JsonConvert.DeserializeObject<DtoBleCommandResult>(responseContent) ?? throw new InvalidDataException($"Could not parse {responseContent} to {nameof(DtoBleCommandResult)}");
            // Success is unknown as the response is not known but display success false so result message is displayed in UI
            commandResult.Success = false;
            return commandResult;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to pair key.");
            return new()
            {
                ResultMessage = GetErrorMessage(ex, PairKeyTimeout),
                ErrorType = ErrorType.Unknown,
                Success = false,
            };
        }

    }

    public Task SetScheduledCharging(int carId, DateTimeOffset? chargingStartTime)
    {
        throw new NotImplementedException();
    }

    public Task SetChargeLimit(int carId, int limitSoC)
    {
        throw new NotImplementedException();
    }

    public async Task CheckBleApiVersionCompatibilities()
    {
        logger.LogTrace("{method}()", nameof(CheckBleApiVersionCompatibilities));
        var hosts = settings.Cars
            .Where(c => c.UseBle)
            .Select(c => c.BleApiBaseUrl)
            .Distinct().ToList();
        foreach (var host in hosts)
        {
            await CheckBleApiVersionCompatibility(host).ConfigureAwait(false);
        }
    }

    public async Task<string?> CheckBleApiVersionCompatibility(string? host)
    {
        var baseUrl = GetBleBaseUrlFromConfiguredUrl(host);
        if (string.IsNullOrEmpty(baseUrl))
        {
            return "Could not generate a base url based on the inserted URL";
        }
        var url = baseUrl + "Hello/TscVersionCompatibility";
        var client = CreateBleClient();
        using var cancellationTokenSource = new CancellationTokenSource(VersionCheckTimeout);
        var vins = settings.Cars
            .Where(c => c.BleApiBaseUrl == host && c.UseBle && (c.ShouldBeManaged == true))
            .Select(c => c.Vin)
            .ToList();
        try
        {
            var response = await client.GetAsync(url, cancellationTokenSource.Token).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                foreach (var vin in vins)
                {
                    await errorHandlingService.HandleError(nameof(TeslaBleService), nameof(CheckBleApiVersionCompatibilities),
                        $"BLE container with URL {host} not up to date", $"Used for {vin}. Update the BLE container to the latest version",
                        issueKeys.BleVersionCompatibility, vin, null).ConfigureAwait(false);
                }

                return "BLE container is not up to date. Update the BLE container to the latest version.";
            }

            var commandResult = JsonConvert.DeserializeObject<DtoValue<string>>(responseContent);
            if (commandResult == default || commandResult.Value == default)
            {
                foreach (var vin in vins)
                {
                    await errorHandlingService.HandleError(nameof(TeslaBleService), nameof(CheckBleApiVersionCompatibilities),
                        $"BLE container with URL {host} does not respond properly", $"Used for {vin}. Could not get value from {responseContent}",
                        issueKeys.BleVersionCompatibility, vin, null).ConfigureAwait(false);
                }

                return $"BLE container does not respond properly: {responseContent}";
            }
            var couldParse = Version.TryParse(commandResult.Value, out var bleContainerVersion);
            if (!couldParse || bleContainerVersion == default)
            {
                foreach (var vin in vins)
                {
                    await errorHandlingService.HandleError(nameof(TeslaBleService), nameof(CheckBleApiVersionCompatibilities),
                        $"BLE container with URL {host} does not respond properly", $"Used for {vin}. Could not get version from {commandResult.Value}",
                        issueKeys.BleVersionCompatibility, vin, null).ConfigureAwait(false);
                }

                return $"BLE container does not respond properly. Could not get version from: {commandResult.Value}";
            }

            var correctVersion = new Version(2, 37, 0);
            if (!bleContainerVersion.Equals(correctVersion))
            {
                foreach (var vin in vins)
                {
                    await errorHandlingService.HandleError(nameof(TeslaBleService), nameof(CheckBleApiVersionCompatibilities),
                        $"BLE container with URL {host} has an incompatible version", $"Used for {vin}. Correct version: {correctVersion}; BLE version: {bleContainerVersion}. Update TSC and BLE container to the latest version.",
                        issueKeys.BleVersionCompatibility, vin, null).ConfigureAwait(false);
                }

                return $"BLE container with URL {host} has an incompatible version; Correct version: {correctVersion}; BLE version: {bleContainerVersion}. Update TSC and BLE container to the latest version.";
            }

            foreach (var vin in vins)
            {
                await errorHandlingService.HandleErrorResolved(issueKeys.BleVersionCompatibility, vin).ConfigureAwait(false);
            }

            return null;
        }
        catch (Exception ex)
        {
            foreach (var vin in vins)
            {
                await errorHandlingService.HandleError(nameof(TeslaBleService), nameof(CheckBleApiVersionCompatibilities),
                    $"BLE container with URL {host} not reachable", $"Used for {vin}. Looks like the url is not correct or BLE container is not online.",
                    issueKeys.BleVersionCompatibility, vin, ex.StackTrace).ConfigureAwait(false);
            }
            return "BLE container is not reachable. Looks like the url is not correct or BLE container is not online.";
        }
    }

    public List<DtoBleContainer> GetBleContainers()
    {
        logger.LogTrace("{method}()", nameof(GetBleContainers));
        return settings.Cars
            .Where(c => c.UseBle && !string.IsNullOrWhiteSpace(c.BleApiBaseUrl))
            .GroupBy(c => c.BleApiBaseUrl!)
            .Select(g => new DtoBleContainer
            {
                BleApiBaseUrl = g.Key,
                CarNames = g.Select(c => c.Name ?? c.Vin).ToList(),
            })
            .ToList();
    }

    public async Task<Stream?> DownloadLogs(string bleApiBaseUrl)
    {
        logger.LogTrace("{method}({bleApiBaseUrl})", nameof(DownloadLogs), bleApiBaseUrl);
        // Only allow URLs that are actually configured on a BLE enabled car to avoid being used as a request proxy.
        var isConfigured = settings.Cars.Any(c => c.UseBle && c.BleApiBaseUrl == bleApiBaseUrl);
        if (!isConfigured)
        {
            logger.LogWarning("BLE base url {bleApiBaseUrl} is not configured for any car. Not downloading logs.", bleApiBaseUrl);
            return null;
        }
        var baseUrl = GetBleBaseUrlFromConfiguredUrl(bleApiBaseUrl);
        if (string.IsNullOrEmpty(baseUrl))
        {
            return null;
        }
        var url = baseUrl + "Debug/DownloadInMemoryLogs";
        logger.LogTrace("Ble Url: {bleUrl}", url);
        var client = CreateBleClient();
        using var cancellationTokenSource = new CancellationTokenSource(DownloadLogsTimeout);
        try
        {
            var response = await client.GetAsync(url, cancellationTokenSource.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to download BLE logs from {url}. StatusCode: {statusCode}", url, response.StatusCode);
                return null;
            }
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            return new MemoryStream(bytes);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download BLE logs from {url}.", url);
            return null;
        }
    }

    private async Task<DtoBleCommandResult> SendCommandToBle(DtoBleRequest request)
    {
        logger.LogTrace("{method}({@request})", nameof(SendCommandToBle), request);
        var bleBaseUrl = GetBleBaseUrl(request.Vin);
        if (string.IsNullOrWhiteSpace(bleBaseUrl))
        {
            return new DtoBleCommandResult()
            {
                Success = false,
                ResultMessage = "BLE Base URL is not set. Set a BLE URL in your base configuration.",
                ErrorType = ErrorType.TscConfiguration,
            };
        }
        bleBaseUrl += "Command/ExecuteCommand";
        var queryString = HttpUtility.ParseQueryString(string.Empty);
        queryString.Add("vin", request.Vin);
        queryString.Add("command", request.CommandName);
        if (!string.IsNullOrEmpty(request.Domain))
        {
            queryString.Add("domain", request.Domain);
        }
        var url = $"{bleBaseUrl}?{queryString}";
        logger.LogTrace("Ble Url: {bleUrl}", url);
        logger.LogTrace("Parameters: {@parameters}", request.Parameters);
        var client = CreateBleClient();
        using var cancellationTokenSource = new CancellationTokenSource(CommandTimeout);
        try
        {
            var response = await client.PostAsJsonAsync(url, request.Parameters, cancellationTokenSource.Token).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to send command to BLE. StatusCode: {statusCode} {responseContent}", response.StatusCode, responseContent);
                throw new InvalidOperationException();
            }
            var commandResult = JsonConvert.DeserializeObject<DtoBleCommandResult>(responseContent) ?? throw new InvalidDataException($"Could not parse {responseContent} to {nameof(DtoBleCommandResult)}");
            return commandResult;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send ble command.");
            return new DtoBleCommandResult()
            {
                ResultMessage = GetErrorMessage(ex, CommandTimeout),
                Success = false,
                ErrorType = ErrorType.Unknown,
            };
        }

    }

    /// <summary>
    /// Creates a client whose connections are pooled and rotated by <see cref="IHttpClientFactory"/>. Must not be
    /// disposed: the returned instance is cheap, the underlying handler is shared and outlives it.
    /// </summary>
    private HttpClient CreateBleClient() => httpClientFactory.CreateClient(StaticConstants.HttpClientNameBle);

    /// <summary>
    /// The result message is displayed in the UI, so a cancellation caused by our own timeout has to be named
    /// explicitly instead of surfacing the useless "A task was canceled." of <see cref="CancellationTokenSource"/>.
    /// </summary>
    private static string GetErrorMessage(Exception exception, TimeSpan timeout) => exception is OperationCanceledException
        ? $"BLE request timed out after {timeout.TotalSeconds:0.#} seconds."
        : exception.Message;

    private string? GetBleBaseUrl(string vin)
    {
        var car = settings.Cars.First(c => c.Vin == vin);
        return GetBleBaseUrlFromConfiguredUrl(car.BleApiBaseUrl);
    }

    private static string? GetBleBaseUrlFromConfiguredUrl(string? bleApiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(bleApiBaseUrl))
        {
            return null;
        }
        if (!bleApiBaseUrl.EndsWith("/"))
        {
            bleApiBaseUrl += "/";
        }
        if (!bleApiBaseUrl.EndsWith("/api/"))
        {
            bleApiBaseUrl += "api/";
        }
        return bleApiBaseUrl;
    }
}
