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

    /// <summary>
    /// Overload: returns segments inside [startDate, endDate] split by boundaries,
    /// PLUS all values outside the boundaries (unchanged except where they cross a boundary,
    /// in which case they are cut exactly at the boundary).
    /// </summary>
    public (List<TLeft> SplitLeft, List<TRight> SplitRight) SplitByBoundaries<TLeft, TRight>(
        List<TLeft> leftItems,
        List<TRight> rightItems,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        bool includeOuterSegments)
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

        // --- inner segmentation as before (only inside [startDate, endDate]) ---

        var boundaryTimes = new[]
            {
            startDate,
            endDate,
        }
            .Concat(leftItems.SelectMany(item => new[] { item.ValidFrom, item.ValidTo }))
            .Concat(rightItems.SelectMany(item => new[] { item.ValidFrom, item.ValidTo }))
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

        if (!includeOuterSegments)
        {
            return (splitLeft, splitRight);
        }

        // --- add segments outside [startDate, endDate] ---

        void AddOuterSegments<T>(List<T> sourceItems, List<T> target)
            where T : ValidFromToBase, new()
        {
            foreach (var item in sourceItems)
            {
                // Entirely before or entirely after -> return as is
                if (item.ValidTo <= startDate || item.ValidFrom >= endDate)
                {
                    var clone = new T
                    {
                        ValidFrom = item.ValidFrom,
                        ValidTo = item.ValidTo,
                    };
                    CopyOtherProperties(item, clone);
                    target.Add(clone);
                    continue;
                }

                // Part before the start boundary
                if (item.ValidFrom < startDate)
                {
                    var before = new T
                    {
                        ValidFrom = item.ValidFrom,
                        ValidTo = startDate,
                    };
                    CopyOtherProperties(item, before);
                    target.Add(before);
                }

                // Part after the end boundary
                if (item.ValidTo > endDate)
                {
                    var after = new T
                    {
                        ValidFrom = endDate,
                        ValidTo = item.ValidTo,
                    };
                    CopyOtherProperties(item, after);
                    target.Add(after);
                }
            }
        }

        AddOuterSegments(leftItems, splitLeft);
        AddOuterSegments(rightItems, splitRight);

        // Optional: keep overall result ordered by time
        splitLeft = splitLeft
            .OrderBy(x => x.ValidFrom)
            .ThenBy(x => x.ValidTo)
            .ToList();

        splitRight = splitRight
            .OrderBy(x => x.ValidFrom)
            .ThenBy(x => x.ValidTo)
            .ToList();

        return (splitLeft, splitRight);
    }

    // Shallow‐copies all properties except ValidFrom/ValidTo
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
