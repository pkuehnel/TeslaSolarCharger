namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ISunCalculator
{
    DateTimeOffset? CalculateSunset(double latitude, double longitude, DateTimeOffset date);
    DateTimeOffset? CalculateSunrise(double latitude, double longitude, DateTimeOffset date);
}
