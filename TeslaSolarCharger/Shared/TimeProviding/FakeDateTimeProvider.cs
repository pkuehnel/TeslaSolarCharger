using TeslaSolarCharger.Shared.Contracts;

namespace TeslaSolarCharger.Shared.TimeProviding;

public class FakeDateTimeProvider : IDateTimeProvider
{
    private readonly DateTime _dateTime;

    public FakeDateTimeProvider(DateTime dateTime)
    {
        _dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Local);
    }

    public DateTime Now()
    {
        return _dateTime;
    }

    public DateTime UtcNow()
    {
        return _dateTime.ToUniversalTime();
    }

    public DateTimeOffset DateTimeOffSetNow()
    {
        var offset = TimeSpan.Zero;
        if (_dateTime.Kind != DateTimeKind.Utc)
        {
            offset = TimeZoneInfo.Local.GetUtcOffset(_dateTime);
        }

        return new DateTimeOffset(_dateTime, offset);
    }

    public DateTimeOffset DateTimeOffSetUtcNow()
    {
        return new DateTimeOffset(_dateTime).ToUniversalTime();
    }
}
