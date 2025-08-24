using TeslaSolarCharger.Server.Helper.Contracts;

namespace TeslaSolarCharger.Server.Helper;

public class TimestampHelper : ITimestampHelper
{
    private readonly ILogger<TimestampHelper> _logger;

    public TimestampHelper(ILogger<TimestampHelper> logger)
    {
        _logger = logger;
    }

    public List<DateTimeOffset> GenerateSlicedTimeStamps(
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        TimeSpan sliceLength)
    {
        _logger.LogTrace("{method}({startDate}, {endDate}, {sliceLength})",
            nameof(GenerateSlicedTimeStamps), startDate, endDate, sliceLength);
        if (sliceLength <= TimeSpan.Zero)
            throw new ArgumentException("Slice length must be greater than zero.", nameof(sliceLength));

        // 1 day
        var oneDay = TimeSpan.FromHours(24);

        // sliceLength cannot exceed 24h
        if (sliceLength > oneDay)
            throw new ArgumentException("Slice length cannot exceed 24 hours.", nameof(sliceLength));

        var oneHour = TimeSpan.FromHours(1);
        // 24h must be an exact multiple of sliceLength
        if (oneHour.Ticks % sliceLength.Ticks != 0)
            throw new ArgumentException(
                "1 hour must be an exact multiple of sliceLength.", nameof(sliceLength));

        if (startDate > endDate)
            throw new ArgumentOutOfRangeException(
                nameof(startDate), "Start date must be on or before end date.");

        if (!IsAlignedToHourGrid(startDate, sliceLength))
        {
            throw new ArgumentException(
                $"Start date must align with slice grid to hit full hours. " +
                $"Minutes/seconds/milliseconds must form a multiple of {sliceLength}.",
                nameof(startDate));
        }

        var totalSpan = endDate - startDate;

        // Ensure the total span is an exact multiple of sliceLength
        if (totalSpan.Ticks % sliceLength.Ticks != 0)
            throw new ArgumentException(
                "The time span between startDate and endDate must be an exact multiple of sliceLength.");

        var slicedTimeStamps = new List<DateTimeOffset>();
        for (var current = startDate; current < endDate; current = current.Add(sliceLength))
        {
            slicedTimeStamps.Add(current);
        }
        return slicedTimeStamps;
    }

    private bool IsAlignedToHourGrid(DateTimeOffset dateTime, TimeSpan sliceLength)
    {
        var timeOfDay = dateTime.TimeOfDay;
        return timeOfDay.Ticks % sliceLength.Ticks == 0;
    }
}
