using Newtonsoft.Json;
using System.Net;
using System.Web;
using TeslaSolarCharger.Server.Dtos.Ble;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
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

    public async Task<string> PairKey(string vin)
    {
        logger.LogTrace("{method}({vin})", nameof(PairKey), vin);
        var bleBaseUrl = configurationWrapper.BleBaseUrl();
        if (string.IsNullOrWhiteSpace(bleBaseUrl))
        {
            throw new InvalidOperationException("BLE Base Url is not set.");
        }
        if (!bleBaseUrl.EndsWith("/"))
        {
            bleBaseUrl += "/";
        }
        bleBaseUrl += "Pairing/PairCar";
        var queryString = HttpUtility.ParseQueryString(string.Empty);
        queryString.Add("vin", vin);
        var url = $"{bleBaseUrl}?{queryString}";
        logger.LogTrace("Ble Url: {bleUrl}", url);
        using var client = new HttpClient();
        var response = await client.GetAsync(url).ConfigureAwait(false);
        var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Failed to send command to BLE. StatusCode: {statusCode} {responseContent}", response.StatusCode, responseContent);
            throw new InvalidOperationException();
        }
        return responseContent;
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
                Message = "BLE Base Url is not set.",
                StatusCode = HttpStatusCode.BadRequest,
            };
        }
        if (!bleBaseUrl.EndsWith("/"))
        {
            bleBaseUrl += "/";
        }
        bleBaseUrl += "Command/ExecuteCommand";
        var queryString = HttpUtility.ParseQueryString(string.Empty);
        queryString.Add("vin", request.Vin);
        queryString.Add("command", request.CommandName);
        var url = $"{bleBaseUrl}?{queryString}";
        logger.LogTrace("Ble Url: {bleUrl}", url);
        logger.LogTrace("Parameters: {@parameters}", request.Parameters);
        using var client = new HttpClient();
        var response = await client.PostAsJsonAsync(url, request.Parameters).ConfigureAwait(false);
        var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Failed to send command to BLE. StatusCode: {statusCode} {responseContent}", response.StatusCode, responseContent);
            throw new InvalidOperationException();
        }
        var result = JsonConvert.DeserializeObject<DtoBleResult>(responseContent);
        return result ?? throw new InvalidDataException($"Could not parse {responseContent} to {nameof(DtoBleResult)}");
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
