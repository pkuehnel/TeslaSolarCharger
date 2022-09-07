using TeslaSolarCharger.Shared.Contracts;

namespace TeslaSolarCharger.Shared.TimeProviding;

public class FakeDateTimeProvider : IDateTimeProvider
{
    private readonly DateTime _dateTime;

    public FakeDateTimeProvider(DateTime dateTime)
    {
        _dateTime = dateTime;
    }

    public DateTime Now()
    {
        return _dateTime;
    }

    public DateTime UtcNow()
    {
        return _dateTime;
    }
}
