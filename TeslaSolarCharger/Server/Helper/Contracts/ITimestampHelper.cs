namespace TeslaSolarCharger.Server.Helper.Contracts;

public interface ITimestampHelper
{
    List<DateTimeOffset> GenerateSlicedTimeStamps(
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        TimeSpan sliceLength);
}
