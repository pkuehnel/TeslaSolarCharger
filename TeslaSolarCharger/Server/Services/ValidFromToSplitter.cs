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

    /// <summary>
    /// Splits the specified left and right item lists into segments based on all unique boundary dates within the given
    /// date range.
    /// </summary>
    /// <remarks>Boundary dates are determined by combining startDate, endDate, and all ValidFrom/ValidTo
    /// values from both item lists that fall within the specified range. Each segment represents a time interval
    /// between consecutive boundary dates, and only items whose validity covers the entire segment are included. The
    /// returned lists preserve the order of segments as determined by the sorted boundary dates.</remarks>
    /// <typeparam name="TLeft">The type of items in the left list. Must inherit from ValidFromToBase and have a parameterless constructor.</typeparam>
    /// <typeparam name="TRight">The type of items in the right list. Must inherit from ValidFromToBase and have a parameterless constructor.</typeparam>
    /// <param name="leftItems">The list of left items to be segmented. Each item must have valid date boundaries defined by ValidFrom and
    /// ValidTo.</param>
    /// <param name="rightItems">The list of right items to be segmented. Each item must have valid date boundaries defined by ValidFrom and
    /// ValidTo.</param>
    /// <param name="startDate">The start of the date range for segmentation. Must be less than or equal to endDate.</param>
    /// <param name="endDate">The end of the date range for segmentation. Must be greater than or equal to startDate.</param>
    /// <returns>A tuple containing two lists: SplitLeft and SplitRight. Each list contains segments of the original items, split
    /// by all unique boundary dates within the specified range. If no segments are found, the lists will be empty.</returns>
    /// <exception cref="ArgumentException">Thrown when startDate is greater than endDate.</exception>
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
