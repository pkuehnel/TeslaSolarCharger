using Newtonsoft.Json;
using System.Net;
using System.Web;
using TeslaSolarCharger.Server.Dtos.Ble;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Ble;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class TeslaBleService(ILogger<TeslaBleService> logger,
    ISettings settings) : IBleService
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
        var initialRequestedCurrent = car.ChargerRequestedCurrent;
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
            //Default timeout of Tesla Command CLI is 21 seconds.
            client.Timeout = TimeSpan.FromSeconds(22);
            var response = await client.PostAsJsonAsync(url, request.Parameters).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to send command to BLE. StatusCode: {statusCode} {responseContent}", response.StatusCode, responseContent);
                throw new InvalidOperationException();
            }
            var commandResult = JsonConvert.DeserializeObject<DtoBleCommandResult>(responseContent) ?? throw new InvalidDataException($"Could not parse {responseContent} to {nameof(DtoBleCommandResult)}"); ;
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
        var bleUrl = car.BleApiBaseUrl;
        if (string.IsNullOrWhiteSpace(bleUrl))
        {
            return null;
        }
        if (!bleUrl.EndsWith("/"))
        {
            bleUrl += "/";
        }
        if (!bleUrl.EndsWith("/api/"))
        {
            bleUrl += "api/";
        }
        return bleUrl;
    }

    private string GetVinByCarId(int carId)
    {
        var vin = settings.Cars.First(c => c.Id == carId).Vin;
        return vin;
    }
}
