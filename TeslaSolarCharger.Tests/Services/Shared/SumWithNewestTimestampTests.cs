using System;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Dtos.Settings;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Shared;

/// <summary>
/// Adding up the readings of one device, as the pages and the charging logic see them.
/// </summary>
public class SumWithNewestTimestampTests
{
    private static readonly DateTimeOffset ReadAt = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NoReadingsHaveNoSumRatherThanZero()
    {
        Assert.Null(Array.Empty<DtoTimeStampedValue<decimal>>().SumWithNewestTimestamp());
    }

    [Fact]
    public void ASingleReadingStaysAsItIs()
    {
        var sum = new[] { new DtoTimeStampedValue<decimal>(ReadAt, 1500.5m), }.SumWithNewestTimestamp();

        Assert.NotNull(sum);
        Assert.Equal(1500.5m, sum.Value);
        Assert.Equal(ReadAt, sum.Timestamp);
    }

    [Fact]
    public void ReadingsAreAddedUpKeepingTheirSign()
    {
        var sum = new[]
        {
            new DtoTimeStampedValue<decimal>(ReadAt, 2000),
            new DtoTimeStampedValue<decimal>(ReadAt, -500),
            new DtoTimeStampedValue<decimal>(ReadAt, 0.25m),
        }.SumWithNewestTimestamp();

        Assert.Equal(1500.25m, sum!.Value);
    }

    [Fact]
    public void TheSumIsAsRecentAsTheNewestReading()
    {
        var sum = new[]
        {
            new DtoTimeStampedValue<decimal>(ReadAt.AddMinutes(-3), 100),
            new DtoTimeStampedValue<decimal>(ReadAt, 200),
            new DtoTimeStampedValue<decimal>(ReadAt.AddMinutes(-1), 300),
        }.SumWithNewestTimestamp();

        Assert.Equal(ReadAt, sum!.Timestamp);
    }

    [Fact]
    public void HistoricReadingsCanBeAddedUpDirectly()
    {
        //The value handling services keep their readings with a history, which must not get in the way of a sum.
        var sum = new[]
        {
            new DtoHistoricValue<decimal>(ReadAt, 100, 5),
            new DtoHistoricValue<decimal>(ReadAt.AddSeconds(-1), 50, 5),
        }.SumWithNewestTimestamp();

        Assert.Equal(150, sum!.Value);
        Assert.Equal(ReadAt, sum.Timestamp);
        Assert.IsNotType<DtoHistoricValue<decimal>>(sum);
    }
}
