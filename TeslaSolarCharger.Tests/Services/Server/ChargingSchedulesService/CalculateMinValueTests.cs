using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingSchedulesService;

public class CalculateMinValueTests : TestBase
{
    public CalculateMinValueTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    /// <summary>
    /// Tests that CalculateMinValue returns the correct minimum value based on the inputs.
    /// It handles null values as "no limit" or "ignore", returning the non-null value if one exists.
    /// If both are null, it returns null.
    /// If both are present, it returns the smaller of the two.
    /// </summary>
    /// <param name="connectorValue">The value from the connector (or null).</param>
    /// <param name="carValue">The value from the car (or null).</param>
    /// <param name="expectedValue">The expected result.</param>
    [Theory]
    // Both null -> returns null
    [InlineData(null, null, null)]
    // Only connector has value -> returns connector value
    [InlineData(10, null, 10)]
    // Only car has value -> returns car value
    [InlineData(null, 10, 10)]
    // Both have values, connector is smaller -> returns connector value
    [InlineData(10, 20, 10)]
    // Both have values, car is smaller -> returns car value
    [InlineData(20, 10, 10)]
    // Both have values, equal -> returns value
    [InlineData(10, 10, 10)]
    // Zero handling
    [InlineData(0, 10, 0)]
    [InlineData(10, 0, 0)]
    [InlineData(0, null, 0)]
    [InlineData(null, 0, 0)]
    public void CalculateMinValue_ReturnsCorrectValue(int? connectorValue, int? carValue, int? expectedValue)
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        // Act
        var result = service.CalculateMinValue(connectorValue, carValue);

        // Assert
        Assert.Equal(expectedValue, result);
    }
}
