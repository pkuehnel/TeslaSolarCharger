namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ISunCalculator
{
    DateTimeOffset? NextSunset(double latitude, double longitude, DateTimeOffset from, int maxFutureDays);
    DateTimeOffset? NextSunrise(double latitude, double longitude, DateTimeOffset from, int maxFutureDays);
}
