using SolarTeslaCharger.Shared.Contracts;

namespace SolarTeslaCharger.Shared.TimeProviding;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime Now()
    {
        return DateTime.Now;
    }
}