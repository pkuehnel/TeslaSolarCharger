using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Plugins.SmaEnergymeter;
using Plugins.SmaEnergymeter.Controllers;
using Plugins.SmaEnergymeter.Dtos;
using Plugins.SmaEnergymeter.Services;
using System;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.SmaEnergymeterPlugin;

/// <summary>
/// The energy meter values are received via multicast. When the multicast stops arriving (e.g. network issue), the plugin
/// must not keep serving the last values as current ones, or consumers like TSC use a frozen grid power.
/// </summary>
public class CurrentPowerServiceTests
{
    private const uint SerialNumber = 3016905369;
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 3, 0, 0, TimeSpan.Zero);

    private readonly SharedValues _sharedValues = new();

    private CurrentPowerService CreateService() =>
        new(NullLogger<CurrentPowerService>.Instance, _sharedValues, new FixedTimeProvider(Now));

    private CurrentPowerController CreateController() =>
        new(CreateService()) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext(), }, };

    private DtoEnergyMeterValue AddValue(uint serialNumber, TimeSpan age, int overageW = -1500)
    {
        var value = new DtoEnergyMeterValue
        {
            OverageW = overageW,
            LastValuesFrom = (Now - age).UtcDateTime,
        };
        _sharedValues.EnergyMeterValues.Add(serialNumber, value);
        return value;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    public void GetAllValues_ReturnsValues_WhenNotOlderThanMaxAge(int ageSeconds)
    {
        var value = AddValue(SerialNumber, TimeSpan.FromSeconds(ageSeconds));

        Assert.Same(value, CreateService().GetAllValues(SerialNumber));
    }

    [Theory]
    [InlineData(10.001)]
    [InlineData(60)]
    [InlineData(3600)]
    public void GetAllValues_ReturnsNull_WhenOlderThanMaxAge(double ageSeconds)
    {
        AddValue(SerialNumber, TimeSpan.FromSeconds(ageSeconds));

        Assert.Null(CreateService().GetAllValues(SerialNumber));
    }

    [Fact]
    public void GetAllValues_ReturnsNull_WhenSerialNumberUnknown()
    {
        AddValue(SerialNumber, TimeSpan.Zero);

        Assert.Null(CreateService().GetAllValues(123));
    }

    [Fact]
    public void GetAllValues_ReturnsNull_WhenNoValuesReceivedYet()
    {
        Assert.Null(CreateService().GetAllValues(null));
        Assert.Null(CreateService().GetAllValues(SerialNumber));
    }

    [Fact]
    public void GetAllValues_WithoutSerialNumber_UsesLastMeter()
    {
        AddValue(1, TimeSpan.Zero);
        var last = AddValue(2, TimeSpan.Zero);

        Assert.Same(last, CreateService().GetAllValues(null));
    }

    [Fact]
    public void GetAllValues_WithoutSerialNumber_ReturnsNull_WhenLastMeterValuesTooOld()
    {
        AddValue(1, TimeSpan.Zero);
        AddValue(2, TimeSpan.FromMinutes(1));

        Assert.Null(CreateService().GetAllValues(null));
    }

    [Fact]
    public void GetCurrentPower_ReturnsOverage_WhenValuesCurrent()
    {
        AddValue(SerialNumber, TimeSpan.FromSeconds(1), overageW: 2300);

        Assert.Equal(2300, CreateService().GetCurrentPower(SerialNumber));
    }

    [Fact]
    public void GetCurrentPower_ReturnsNull_WhenValuesTooOld()
    {
        AddValue(SerialNumber, TimeSpan.FromMinutes(1));

        Assert.Null(CreateService().GetCurrentPower(SerialNumber));
    }

    [Fact]
    public void Controller_GetAllValues_ReturnsValues_WhenCurrent()
    {
        var value = AddValue(SerialNumber, TimeSpan.FromSeconds(1));

        var result = CreateController().GetAllValues(SerialNumber);

        Assert.Same(value, result.Value);
    }

    [Fact]
    public void Controller_GetAllValues_Returns503_WhenValuesTooOld()
    {
        AddValue(SerialNumber, TimeSpan.FromMinutes(1));

        var result = CreateController().GetAllValues(SerialNumber);

        Assert.Null(result.Value);
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
    }

    [Fact]
    public void Controller_GetPower_ReturnsPower_WhenCurrent()
    {
        AddValue(SerialNumber, TimeSpan.FromSeconds(1), overageW: -800);

        var result = CreateController().GetPower(SerialNumber);

        Assert.Equal(-800, result.Value);
    }

    [Fact]
    public void Controller_GetPower_Returns503_WhenNoValues()
    {
        var result = CreateController().GetPower(null);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
