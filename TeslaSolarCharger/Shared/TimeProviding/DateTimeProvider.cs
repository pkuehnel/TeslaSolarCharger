using TeslaSolarCharger.Shared.Contracts;

namespace TeslaSolarCharger.Shared.TimeProviding;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime Now()
    {
        return DateTime.Now;
    }

    public DateTime UtcNow()
    {
        return DateTime.UtcNow;
    }

    public DateTimeOffset DateTimeOffSetNow()
    {
        return DateTimeOffset.Now;
    }
}
