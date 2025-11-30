using System;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.GridPrice.Dtos;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingSchedulesService;

public class ChargingScheduleServiceGetCopyTests : TestBase
{
    public ChargingScheduleServiceGetCopyTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    /// <summary>
    /// Verifies that GetCopy correctly copies all properties from the original Price object
    /// and ensures that ValidFrom and ValidTo are converted to UTC (TimeSpan.Zero offset).
    /// </summary>
    [Theory]
    // Standard case with positive values and non-UTC positive offset
    [InlineData(100.5, 50.25, "2023-01-01T12:00:00+02:00", "2023-01-01T13:00:00+02:00")]
    // UTC input
    [InlineData(10.0, 5.0, "2023-06-15T10:00:00Z", "2023-06-15T11:00:00Z")]
    // Negative offset
    [InlineData(99.99, 11.11, "2023-06-15T10:00:00-05:00", "2023-06-15T11:00:00-05:00")]
    // Zero values
    [InlineData(0, 0, "2023-01-01T12:00:00+00:00", "2023-01-01T13:00:00+00:00")]
    // Negative prices (assuming system allows negative prices, e.g. feed-in or negative grid pricing)
    [InlineData(-10, -5, "2023-12-31T23:00:00+01:00", "2024-01-01T00:00:00+01:00")]
    // Large values
    [InlineData(1000000, 500000, "2023-01-01T12:00:00+01:00", "2023-01-01T13:00:00+01:00")]
    public void GetCopy_CorrectlyCopiesPropertiesAndResetsOffset(
        double gridPriceDouble,
        double solarPriceDouble,
        string validFromStr,
        string validToStr)
    {
        // Arrange
        var gridPrice = (decimal)gridPriceDouble;
        var solarPrice = (decimal)solarPriceDouble;
        var validFrom = DateTimeOffset.Parse(validFromStr);
        var validTo = DateTimeOffset.Parse(validToStr);

        var originalPrice = new Price
        {
            GridPrice = gridPrice,
            SolarPrice = solarPrice,
            ValidFrom = validFrom,
            ValidTo = validTo
        };

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        // Act
        var result = service.GetCopy(originalPrice);

        // Assert
        Assert.NotNull(result);
        Assert.NotSame(originalPrice, result); // Ensure it's a new instance

        Assert.Equal(gridPrice, result.GridPrice);
        Assert.Equal(solarPrice, result.SolarPrice);

        // Verify the times represent the same instant in time
        Assert.Equal(validFrom.UtcDateTime, result.ValidFrom.UtcDateTime);
        Assert.Equal(validTo.UtcDateTime, result.ValidTo.UtcDateTime);

        // Verify the offset is zero (UTC)
        Assert.Equal(TimeSpan.Zero, result.ValidFrom.Offset);
        Assert.Equal(TimeSpan.Zero, result.ValidTo.Offset);
    }
}
