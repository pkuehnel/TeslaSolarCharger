﻿using Newtonsoft.Json;
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
        using var client = new HttpClient();
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

            var correctVersion = new Version(2, 35, 0);
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
