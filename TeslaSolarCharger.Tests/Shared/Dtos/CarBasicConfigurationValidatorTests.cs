using TeslaSolarCharger.Shared.Dtos;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Shared.Dtos;

public class CarBasicConfigurationValidatorTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    private readonly CarBasicConfigurationValidator _validator = new();

    [Fact]
    public void Validate_ValidConfiguration_ShouldNotHaveErrors()
    {
        var config = new CarBasicConfiguration
        {
            BleApiBaseUrl = "http://valid-url.com",
            SwitchOnAtCurrent = 10,
            SwitchOffAtCurrent = 6,
            MaximumAmpere = 16,
            MinimumAmpere = 6,
            UsableEnergy = 60,
            ChargingPriority = 1,
            MaximumPhases = 3,
            ShouldBeManaged = true,
            Name = "Test Car",
            Vin = "1234567890ABCDEF"
        };

        var result = _validator.Validate(config);

        Assert.True(result.IsValid, string.Join(", ", result.Errors));
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://invalid-scheme.com")]
    [InlineData("file://etc/passwd")]
    [InlineData("javascript:alert(1)")]
    public void Validate_InvalidBleApiBaseUrl_ShouldHaveErrors(string invalidUrl)
    {
        var config = new CarBasicConfiguration
        {
            BleApiBaseUrl = invalidUrl,
            ShouldBeManaged = true,
            SwitchOnAtCurrent = 10,
            SwitchOffAtCurrent = 6,
            MaximumAmpere = 16,
            MinimumAmpere = 6,
            UsableEnergy = 60,
            ChargingPriority = 1,
            MaximumPhases = 3,
            Name = "Test Car",
            Vin = "1234567890ABCDEF"
        };

        var result = _validator.Validate(config);

        Assert.False(result.IsValid, "Validator should fail for invalid URLs");
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CarBasicConfiguration.BleApiBaseUrl));
    }
}
