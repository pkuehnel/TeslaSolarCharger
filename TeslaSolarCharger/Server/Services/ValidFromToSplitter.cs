using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Server.Services;

public class ValidFromToSplitter : IValidFromToSplitter
{
    private readonly ILogger<ValidFromToSplitter> _logger;

    public ValidFromToSplitter(ILogger<ValidFromToSplitter> logger)
    {
        _logger = logger;
    }
    public (List<TLeft> SplitLeft, List<TRight> SplitRight) SplitByBoundaries<TLeft, TRight>(List<TLeft> leftItems, List<TRight> rightItems, DateTimeOffset startDate, DateTimeOffset endDate)
        where TLeft : ValidFromToBase, new()
        where TRight : ValidFromToBase, new()
    {
        _logger.LogTrace("{method}()", nameof(SplitByBoundaries));
        if (startDate > endDate)
        {
            throw new ArgumentException(
                "startDate must be less than or equal to endDate",
                nameof(startDate)
            );
        }
        
        var splitLeft = new List<TLeft>();
        var splitRight = new List<TRight>();
        var boundaryTimes = new[]
            {
                startDate,
                endDate,
            }
            .Concat(leftItems.SelectMany(item => new[] { item.ValidFrom, item.ValidTo, }))
            .Concat(rightItems.SelectMany(item => new[] { item.ValidFrom, item.ValidTo, }))
            .Where(time => time >= startDate && time <= endDate)
            .Distinct()
            .OrderBy(time => time)
            .ToList();

        for (var i = 0; i < boundaryTimes.Count - 1; i++)
        {
            var segmentStart = boundaryTimes[i];
            var segmentEnd = boundaryTimes[i + 1];

            var originalLeft = leftItems
                .FirstOrDefault(item =>
                    item.ValidFrom <= segmentStart &&
                    item.ValidTo >= segmentEnd);

            var originalRight = rightItems
                .FirstOrDefault(item =>
                    item.ValidFrom <= segmentStart &&
                    item.ValidTo >= segmentEnd);

            if (originalLeft != null)
            {
                var slice = new TLeft
                {
                    ValidFrom = segmentStart,
                    ValidTo = segmentEnd,
                };
                CopyOtherProperties(originalLeft, slice);
                splitLeft.Add(slice);
            }

            if (originalRight != null)
            {
                var slice = new TRight
                {
                    ValidFrom = segmentStart,
                    ValidTo = segmentEnd,
                };
                CopyOtherProperties(originalRight, slice);
                splitRight.Add(slice);
            }
        }

        return (splitLeft, splitRight);
    }

    // Shallow‐copies all properties except StartTime/EndTime
    private void CopyOtherProperties<T>(T source, T target)
    {
        var props = typeof(T)
            .GetProperties()
            .Where(p =>
                p.CanRead && p.CanWrite &&
                p.Name != nameof(ValidFromToBase.ValidFrom) &&
                p.Name != nameof(ValidFromToBase.ValidTo));

        foreach (var prop in props)
        {
            var value = prop.GetValue(source);
            prop.SetValue(target, value);
        }
    }
}
