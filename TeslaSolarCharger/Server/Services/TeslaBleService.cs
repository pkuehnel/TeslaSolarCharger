using Newtonsoft.Json;
using PkSoftwareService.Custom.Backend.Ble;
using System.Net;
using System.Web;
using TeslaSolarCharger.Server.Dtos.Ble;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Ble;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class TeslaBleService(ILogger<TeslaBleService> logger,
    ISettings settings,
    IErrorHandlingService errorHandlingService,
    IIssueKeys issueKeys) : IBleService
{
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
        
        bleBaseUrl += BleApiRoutes.PairCar;
        var queryString = HttpUtility.ParseQueryString(string.Empty);
        queryString.Add(BleApiRoutes.VinQueryParam, vin);
        queryString.Add(BleApiRoutes.ApiRoleQueryParam, apiRole);
        AddAdapterQueryParameter(queryString, vin);
        var url = $"{bleBaseUrl}?{queryString}";
        logger.LogTrace("Ble Url: {bleUrl}", url);
        using var client = new HttpClient();
        //Pairing stops the worker of the target adapter, waits for the adapter ownership guard and then runs
        //tesla-control, so it needs more headroom than a normal command but must not hang forever.
        client.Timeout = TimeSpan.FromSeconds(60);
        try
        {
            var response = await client.GetAsync(url).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
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
                ResultMessage = ex.Message,
                ErrorType = ErrorType.Unknown,
                Success = false,
            };
        }
        
    }

    public async Task<DtoBleBeaconScanResult> GetBeaconScanResults(string? host, string? adapter, List<string> vins, int? keepWarmSeconds)
    {
        logger.LogTrace("{method}({host}, {adapter}, {@vins}, {keepWarmSeconds})", nameof(GetBeaconScanResults), host, adapter, vins, keepWarmSeconds);
        var bleBaseUrl = GetBleBaseUrlFromConfiguredUrl(host);
        if (string.IsNullOrWhiteSpace(bleBaseUrl))
        {
            return new DtoBleBeaconScanResult
            {
                Success = false,
                Outcome = BleCommandOutcome.InvalidRequest,
                ResultMessage = "BLE Base URL is not set. Set a BLE URL in your base configuration.",
            };
        }
        var queryString = HttpUtility.ParseQueryString(string.Empty);
        if (!string.IsNullOrWhiteSpace(adapter))
        {
            queryString.Add(BleApiRoutes.AdapterQueryParam, adapter);
        }
        if (keepWarmSeconds != default)
        {
            queryString.Add(BleApiRoutes.KeepWarmSecondsQueryParam, keepWarmSeconds.Value.ToString());
        }
        var url = $"{bleBaseUrl}{BleApiRoutes.BeaconScan}?{queryString}";
        logger.LogTrace("Ble Url: {bleUrl}", url);
        using var client = new HttpClient();
        //The container answers within the scan window; the extra headroom covers a worker start.
        client.Timeout = TimeSpan.FromSeconds(29);
        try
        {
            var response = await client.PostAsJsonAsync(url, vins).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                //An old BLE container does not know the endpoint. The version mismatch is surfaced separately; here
                //it only means no presence information, never "car is away".
                return new DtoBleBeaconScanResult
                {
                    Success = false,
                    Outcome = BleCommandOutcome.InvalidRequest,
                    ResultMessage = "The BLE container does not support beacon scans. Update the BLE container to the latest version.",
                };
            }
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to run beacon scan. StatusCode: {statusCode} {responseContent}", response.StatusCode, responseContent);
                return new DtoBleBeaconScanResult
                {
                    Success = false,
                    Outcome = BleCommandOutcome.WorkerError,
                    ResultMessage = $"BLE container answered with {response.StatusCode}: {responseContent}",
                };
            }
            return JsonConvert.DeserializeObject<DtoBleBeaconScanResult>(responseContent)
                   ?? throw new InvalidDataException($"Could not parse {responseContent} to {nameof(DtoBleBeaconScanResult)}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run beacon scan.");
            return new DtoBleBeaconScanResult
            {
                Success = false,
                Outcome = BleCommandOutcome.WorkerError,
                ResultMessage = ex.Message,
            };
        }
    }

    public Task<DtoBleBeaconScanResult> GetBeaconScanResultForVin(string vin)
    {
        logger.LogTrace("{method}({vin})", nameof(GetBeaconScanResultForVin), vin);
        var car = settings.Cars.FirstOrDefault(c => c.Vin == vin);
        if (car == default)
        {
            return Task.FromResult(new DtoBleBeaconScanResult
            {
                Success = false,
                Outcome = BleCommandOutcome.InvalidRequest,
                ResultMessage = $"No car with VIN {vin} is known.",
            });
        }
        return GetBeaconScanResults(car.BleApiBaseUrl, car.BleAdapterAddress, new List<string> { vin }, null);
    }

    public async Task<List<DtoBleAdapter>> GetAdapters(string? host)
    {
        logger.LogTrace("{method}({host})", nameof(GetAdapters), host);
        var bleBaseUrl = GetBleBaseUrlFromConfiguredUrl(host);
        if (string.IsNullOrWhiteSpace(bleBaseUrl))
        {
            return new List<DtoBleAdapter>();
        }
        var url = bleBaseUrl + BleApiRoutes.AdapterList;
        logger.LogTrace("Ble Url: {bleUrl}", url);
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        try
        {
            var response = await client.GetAsync(url).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Could not get Bluetooth adapters from {url}. StatusCode: {statusCode}", url, response.StatusCode);
                return new List<DtoBleAdapter>();
            }
            return JsonConvert.DeserializeObject<List<DtoBleAdapter>>(responseContent) ?? new List<DtoBleAdapter>();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not get Bluetooth adapters from {url}.", url);
            return new List<DtoBleAdapter>();
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
        var url = baseUrl + BleApiRoutes.TscVersionCompatibility;
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(5);
        var vins = settings.Cars
            .Where(c => c.BleApiBaseUrl == host && c.UseBle && (c.ShouldBeManaged == true))
            .Select(c => c.Vin)
            .ToList();
        try
        {
            var response = await client.GetAsync(url).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
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

            var correctVersion = BleCompatibilityVersion.Value;
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
        var url = baseUrl + BleApiRoutes.DownloadInMemoryLogs;
        logger.LogTrace("Ble Url: {bleUrl}", url);
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        try
        {
            var response = await client.GetAsync(url).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to download BLE logs from {url}. StatusCode: {statusCode}", url, response.StatusCode);
                return null;
            }
            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
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
        bleBaseUrl += BleApiRoutes.ExecuteCommand;
        var queryString = HttpUtility.ParseQueryString(string.Empty);
        queryString.Add(BleApiRoutes.VinQueryParam, request.Vin);
        queryString.Add(BleApiRoutes.CommandQueryParam, request.CommandName);
        if (!string.IsNullOrEmpty(request.Domain))
        {
            queryString.Add(BleApiRoutes.DomainQueryParam, request.Domain);
        }
        AddAdapterQueryParameter(queryString, request.Vin);
        if (request.KeepWarmSeconds != default)
        {
            queryString.Add(BleApiRoutes.KeepWarmSecondsQueryParam, request.KeepWarmSeconds.Value.ToString());
        }
        var url = $"{bleBaseUrl}?{queryString}";
        logger.LogTrace("Ble Url: {bleUrl}", url);
        logger.LogTrace("Parameters: {@parameters}", request.Parameters);
        using var client = new HttpClient();
        try
        {
            client.Timeout = TimeSpan.FromSeconds(29);
            var response = await client.PostAsJsonAsync(url, request.Parameters).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
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
                ResultMessage = ex.Message,
                Success = false,
                ErrorType = ErrorType.Unknown,
            };
        }
        
    }

    private string? GetBleBaseUrl(string vin)
    {
        var car = settings.Cars.First(c => c.Vin == vin);
        return GetBleBaseUrlFromConfiguredUrl(car.BleApiBaseUrl);
    }

    /// <summary>
    /// Adds the car's adapter selection to the request. Cars without a selection use the container's default adapter,
    /// which is exactly the behaviour of BLE containers that do not know the parameter yet.
    /// </summary>
    private void AddAdapterQueryParameter(System.Collections.Specialized.NameValueCollection queryString, string vin)
    {
        var adapter = settings.Cars.FirstOrDefault(c => c.Vin == vin)?.BleAdapterAddress;
        if (!string.IsNullOrWhiteSpace(adapter))
        {
            queryString.Add(BleApiRoutes.AdapterQueryParam, adapter);
        }
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
