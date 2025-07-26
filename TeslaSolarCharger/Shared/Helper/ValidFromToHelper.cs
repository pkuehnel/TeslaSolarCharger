using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Helper.Contracts;

namespace TeslaSolarCharger.Shared.Helper;

public class ValidFromToHelper : IValidFromToHelper
{
    public Dictionary<DateTimeOffset, decimal> GetHourlyAverages<T>(
        IEnumerable<T> entries,
        DateTimeOffset from,
        DateTimeOffset to,
        Func<T, decimal> valueSelector,
        bool treatNonOverlappingAsZero
    ) where T : ValidFromToBase
    {
        var startOfFirstHour = new DateTimeOffset(
            from.Year,
            from.Month,
            from.Day,
            from.Hour,
            0,
            0,
            from.Offset
        );
        var totalHours = (int)Math.Ceiling((to - from).TotalHours);
        var hourlyAverages = new Dictionary<DateTimeOffset, decimal>();

        for (var hourIndex = 0; hourIndex < totalHours; hourIndex++)
        {
            var hourStart = startOfFirstHour.AddHours(hourIndex);
            var hourEnd = hourStart.AddHours(1);

            // ReSharper disable once PossibleMultipleEnumeration
            var overlappingEntries = entries
                .Where(entry => entry.ValidFrom < hourEnd && entry.ValidTo > hourStart)
                .ToList();

            if (overlappingEntries.Count == 0)
            {
                hourlyAverages[hourStart] = default;
            }
            else
            {
                var weightedSum = overlappingEntries.Sum(entry =>
                {
                    var overlapDurationHours = GetOverLapDurationInHours(entry, hourStart, hourEnd);
                    return valueSelector(entry) * (decimal)overlapDurationHours;
                });

                var totalOverlapHours = treatNonOverlappingAsZero ? 1 : overlappingEntries.Sum(entry =>
                {
                    var overlapDurationHours = GetOverLapDurationInHours(entry, hourStart, hourEnd);
                    return (decimal)overlapDurationHours;
                });

                hourlyAverages[hourStart] = weightedSum / totalOverlapHours;
            }
        }

        return hourlyAverages;
    }

    private static double GetOverLapDurationInHours<T>(T entry, DateTimeOffset hourStart, DateTimeOffset hourEnd)
        where T : ValidFromToBase
    {
        var overlapStartTime = entry.ValidFrom > hourStart
            ? entry.ValidFrom
            : hourStart;
        var overlapEndTime = entry.ValidTo < hourEnd
            ? entry.ValidTo
            : hourEnd;
        var overlapDurationHours = (overlapEndTime - overlapStartTime).TotalHours;
        return overlapDurationHours;
    }
}
