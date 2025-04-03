using Microsoft.EntityFrameworkCore;
using System.Globalization;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.EntityFramework;
using TeslaSolarCharger.Server.Dtos.Solar4CarBackend;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class WeatherDataService(ILogger<WeatherDataService> logger,
    ITeslaSolarChargerContext context,
    IConfigurationWrapper configurationWrapper,
    IBackendApiService backendApiService,
    IDateTimeProvider dateTimeProvider) : IWeatherDataService
{
    public async Task RefreshWeatherData()
    {
        logger.LogTrace("{method}()", nameof(RefreshWeatherData));
        if (!configurationWrapper.IsPredictSolarPowerGenerationEnabled())
        {
            return;
        }
        var weatherData = await GetWeatherData();
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        foreach (var dtoWeatherDatum in weatherData)
        {
            context.SolarRadiations.Add(new SolarRadiation()
            {
                Start = dtoWeatherDatum.Start,
                End = dtoWeatherDatum.End,
                SolarRadiationWhPerM2 = dtoWeatherDatum.SolarRadiationWhPerM2,
                CreatedAt = currentDate,
            });
        }
        await context.SaveChangesAsync();
    }

    private async Task<List<DtoWeatherDatum>> GetWeatherData()
    {
        logger.LogTrace("{method}()", nameof(GetWeatherData));
        var weatherData = new List<DtoWeatherDatum>();
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        //FromDate needs to be the start of the current hour
        currentDate = new DateTimeOffset(
            currentDate.Year,
            currentDate.Month,
            currentDate.Day,
            currentDate.Hour,
            0,
            0,
            currentDate.Offset);
        var homeGeofenceLatitude = configurationWrapper.HomeGeofenceLatitude();
        var homeGeofenceLongitude = configurationWrapper.HomeGeofenceLongitude();
        var token = await context.BackendTokens.SingleOrDefaultAsync().ConfigureAwait(false);
        if (token == default)
        {
            throw new InvalidOperationException("Can not radiation data without backend token");
        }
        var weatherDataFromBackend = await backendApiService.SendRequestToBackend<List<DtoWeatherDatum>>(HttpMethod.Get, token.AccessToken, $"WeatherData/GetWeatherData?from={currentDate.ToUnixTimeSeconds()}&to={currentDate.AddHours(24).ToUnixTimeSeconds()}&latitude={homeGeofenceLatitude.ToString(CultureInfo.InvariantCulture)}&longitude={homeGeofenceLongitude.ToString(CultureInfo.InvariantCulture)}", null);
        if (weatherDataFromBackend.HasError)
        {
            logger.LogError("Failed to get weather data from backend: {error}", weatherDataFromBackend.ErrorMessage);
            return weatherData;
        }
        return weatherDataFromBackend.Data ?? throw new InvalidOperationException("Weather data is null although has error is false.");
    }
}
