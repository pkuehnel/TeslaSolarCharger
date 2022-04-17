namespace SmartTeslaAmpSetter.Shared.TimeProviding;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime Now()
    {
        return DateTime.Now;
    }
}