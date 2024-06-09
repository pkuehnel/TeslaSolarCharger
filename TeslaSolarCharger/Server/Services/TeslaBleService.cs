using Newtonsoft.Json;
using System.Net;
using System.Web;
using TeslaSolarCharger.Server.Dtos.Ble;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Ble;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class TeslaBleService(ILogger<TeslaBleService> logger,
    IConfigurationWrapper configurationWrapper,
    ITeslamateApiService teslamateApiService,
    ISettings settings) : IBleService
{
    public async Task StartCharging(string vin)
    {
        logger.LogTrace("{method}({vin})", nameof(StartCharging), vin);
        var request = new DtoBleRequest
        {
            Vin = vin,
            CommandName = "charging-start",
        };
        var result = await SendCommandToBle(request).ConfigureAwait(false);
    }

    public Task WakeUpCar(int carId)
    {
        throw new NotImplementedException();
    }

    public async Task StopCharging(string vin)
    {
        var request = new DtoBleRequest
        {
            Vin = vin,
            CommandName = "charging-stop",
        };
        var result = await SendCommandToBle(request).ConfigureAwait(false);
    }

    public async Task SetAmp(string vin, int amps)
    {
        logger.LogTrace("{method}({vin}, {amps})", nameof(SetAmp), vin, amps);
        var request = new DtoBleRequest
        {
            Vin = vin,
            CommandName = "charging-set-amps",
            Parameters = new List<string> { amps.ToString() },
        };
        var result = await SendCommandToBle(request).ConfigureAwait(false);
    }

    public async Task<DtoBleResult> FlashLights(string vin)
    {
        var request = new DtoBleRequest
        {
            Vin = vin,
            CommandName = "flash-lights",
        };
        var result = await SendCommandToBle(request).ConfigureAwait(false);
        return result;
    }

    public async Task<DtoBleResult> PairKey(string vin)
    {
        logger.LogTrace("{method}({vin})", nameof(PairKey), vin);
        var bleBaseUrl = configurationWrapper.BleBaseUrl();
        if (string.IsNullOrWhiteSpace(bleBaseUrl))
        {
            return new DtoBleResult() { Message = "BLE Base Url is not set.", StatusCode = HttpStatusCode.BadRequest, Success = false, };
        }
        
        bleBaseUrl += "Pairing/PairCar";
        var queryString = HttpUtility.ParseQueryString(string.Empty);
        queryString.Add("vin", vin);
        var url = $"{bleBaseUrl}?{queryString}";
        logger.LogTrace("Ble Url: {bleUrl}", url);
        using var client = new HttpClient();
        try
        {
            var response = await client.GetAsync(url).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new DtoBleResult() { Message = responseContent, StatusCode = response.StatusCode, Success = false, };
            }

            // Success is unknown as the response is not known
            return new DtoBleResult() { Message = responseContent, StatusCode = response.StatusCode, Success = false };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to pair key.");
            return new DtoBleResult() { Message = ex.Message, StatusCode = HttpStatusCode.InternalServerError, Success = false, };
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

    private async Task<DtoBleResult> SendCommandToBle(DtoBleRequest request)
    {
        logger.LogTrace("{method}({@request})", nameof(SendCommandToBle), request);
        var bleBaseUrl = configurationWrapper.BleBaseUrl();
        if (string.IsNullOrWhiteSpace(bleBaseUrl))
        {
            return new DtoBleResult()
            {
                Success = false,
                Message = "BLE Base Url is not set. Set a BLE Url in your base configuration.",
                StatusCode = HttpStatusCode.BadRequest,
            };
        }
        bleBaseUrl += "Command/ExecuteCommand";
        var queryString = HttpUtility.ParseQueryString(string.Empty);
        queryString.Add("vin", request.Vin);
        queryString.Add("command", request.CommandName);
        var url = $"{bleBaseUrl}?{queryString}";
        logger.LogTrace("Ble Url: {bleUrl}", url);
        logger.LogTrace("Parameters: {@parameters}", request.Parameters);
        using var client = new HttpClient();
        try
        {
            var response = await client.PostAsJsonAsync(url, request.Parameters).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to send command to BLE. StatusCode: {statusCode} {responseContent}", response.StatusCode, responseContent);
                throw new InvalidOperationException();
            }
            var commandResult = JsonConvert.DeserializeObject<DtoCommandResult>(responseContent) ?? throw new InvalidDataException($"Could not parse {responseContent} to {nameof(DtoCommandResult)}"); ;
            var result = new DtoBleResult
            {
                StatusCode = response.StatusCode,
                Message = commandResult.ResultMessage ?? string.Empty,
                Success = commandResult.Success,
            };
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send ble command.");
            return new DtoBleResult() { Message = ex.Message, StatusCode = HttpStatusCode.InternalServerError, Success = false, };
        }
        
    }

    private async Task WakeUpCarIfNeeded(int carId, CarStateEnum? carState)
    {
        switch (carState)
        {
            case CarStateEnum.Offline or CarStateEnum.Asleep:
                logger.LogInformation("Wakeup car.");
                await WakeUpCar(carId).ConfigureAwait(false);
                break;
            case CarStateEnum.Suspended:
                logger.LogInformation("Resume logging as is suspended");
                var teslaMateCarId = settings.Cars.First(c => c.Id == carId).TeslaMateCarId;
                if (teslaMateCarId != default)
                {
                    await teslamateApiService.ResumeLogging(teslaMateCarId.Value).ConfigureAwait(false);
                }
                break;
        }
    }

    private string GetVinByCarId(int carId)
    {
        var vin = settings.Cars.First(c => c.Id == carId).Vin;
        return vin;
    }
}
