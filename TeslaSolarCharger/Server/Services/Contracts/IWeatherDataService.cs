namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IWeatherDataService
{
    Task RefreshWeatherData();
}
