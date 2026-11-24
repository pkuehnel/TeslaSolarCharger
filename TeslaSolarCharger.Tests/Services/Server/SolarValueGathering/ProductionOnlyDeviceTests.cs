using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;
using TeslaSolarCharger.Tests.Helper;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server.SolarValueGathering;

/// <summary>
/// Telling a device that only delivers production apart from one whose values the charging logic depends on, and how
/// long a device has been failing.
/// </summary>
public class ProductionOnlyDeviceTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 24, 22, 0, 0, TimeSpan.Zero);
    private DateTimeOffset _now = Start;

    private static IGenericValue<decimal> Device(params (ValueUsage Usage, decimal Value)[] readings) =>
        GenericValueTestDevices.Device(ConfigurationType.TemplateValue, Start, readings: readings);

    [Fact]
    public void ADeviceWithOnlyProductionDeliversOnlyProduction()
    {
        Assert.True(Device((ValueUsage.InverterPower, 0)).DeliversOnlyProduction());
    }

    [Theory]
    [InlineData(ValueUsage.GridPower)]
    [InlineData(ValueUsage.HomeBatteryPower)]
    [InlineData(ValueUsage.HomeBatterySoc)]
    public void ADeviceWithAnyOtherValueDoesNotDeliverOnlyProduction(ValueUsage otherUsage)
    {
        Assert.False(Device((ValueUsage.InverterPower, 3000), (otherUsage, 0)).DeliversOnlyProduction());
        Assert.False(Device((otherUsage, 0)).DeliversOnlyProduction());
    }

    [Fact]
    public void ADeviceWithoutAnyValueIsUnknown()
    {
        Assert.False(Device().DeliversOnlyProduction());
    }

    private DelegateRefreshableValue<decimal> RefreshableDevice(Func<ConcurrentDictionary<ValueKey, decimal>> read)
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.DateTimeOffSetUtcNow()).Returns(() => _now);
        var serviceScopeFactory = new ServiceCollection()
            .AddSingleton(dateTimeProvider.Object)
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
        return new DelegateRefreshableValue<decimal>(serviceScopeFactory, _ => Task.FromResult(read()), TimeSpan.Zero, 10,
            new SourceValueKey(1, ConfigurationType.TemplateValue));
    }

    private static ConcurrentDictionary<ValueKey, decimal> Values(params (ValueUsage Usage, decimal Value)[] values) =>
        new(values.ToDictionary(v => new ValueKey(v.Usage, null, (int)v.Usage), v => v.Value));

    private static async Task RefreshIgnoringError(IRefreshableValue<decimal> device)
    {
        try
        {
            await device.RefreshValueAsync(default);
        }
        catch (InvalidOperationException)
        {
            //The failure is what is tested.
        }
    }

    [Fact]
    public async Task RepeatedFailuresKeepTheTimeOfTheFirstFailure()
    {
        var device = RefreshableDevice(() => throw new InvalidOperationException("offline"));

        await RefreshIgnoringError(device);
        _now = Start.AddHours(5);
        await RefreshIgnoringError(device);

        Assert.True(device.HasError);
        Assert.Equal(Start, device.HasErrorSince);
    }

    [Fact]
    public async Task ASuccessfulReadEndsTheFailureAndTheNextFailureStartsANewOne()
    {
        var fails = true;
        var device = RefreshableDevice(() => fails ? throw new InvalidOperationException("offline") : Values((ValueUsage.InverterPower, 100)));
        await RefreshIgnoringError(device);

        fails = false;
        await device.RefreshValueAsync(default);
        Assert.False(device.HasError);
        Assert.Null(device.HasErrorSince);

        fails = true;
        _now = Start.AddHours(10);
        await RefreshIgnoringError(device);
        Assert.Equal(Start.AddHours(10), device.HasErrorSince);
    }

    [Fact]
    public async Task AValueTheDeviceNoLongerDeliversIsForgotten()
    {
        var values = Values((ValueUsage.InverterPower, 100), (ValueUsage.GridPower, -50));
        var device = RefreshableDevice(() => values);
        await device.RefreshValueAsync(default);
        Assert.False(device.DeliversOnlyProduction());

        values = Values((ValueUsage.InverterPower, 200));
        await device.RefreshValueAsync(default);

        var historicValue = Assert.Single(device.HistoricValues);
        Assert.Equal(ValueUsage.InverterPower, historicValue.Key.ValueUsage);
        Assert.Equal(200, historicValue.Value.Value);
        Assert.True(device.DeliversOnlyProduction());
    }

    [Fact]
    public async Task AFailedReadKeepsTheLastValues()
    {
        var fails = false;
        var device = RefreshableDevice(() => fails ? throw new InvalidOperationException("offline") : Values((ValueUsage.InverterPower, 100)));
        await device.RefreshValueAsync(default);

        fails = true;
        await RefreshIgnoringError(device);

        //Still known as a production only device while it is offline.
        Assert.True(device.DeliversOnlyProduction());
    }

    [Theory]
    [InlineData(-32768, ModbusValueType.Short, true)]
    [InlineData(-32767, ModbusValueType.Short, false)]
    [InlineData(65535, ModbusValueType.UShort, true)]
    [InlineData(65534, ModbusValueType.UShort, false)]
    [InlineData(-2147483648, ModbusValueType.Int, true)]
    [InlineData(-2147483647, ModbusValueType.Int, false)]
    [InlineData(4294967295, ModbusValueType.UInt, true)]
    [InlineData(4294967294, ModbusValueType.UInt, false)]
    [InlineData(0, ModbusValueType.Int, false)]
    //A marker of another type is a normal value.
    [InlineData(65535, ModbusValueType.UInt, false)]
    [InlineData(-32768, ModbusValueType.Int, false)]
    [InlineData(-2147483648, ModbusValueType.Float, false)]
    public void TheNotAvailableMarkerDependsOnTheValueType(double rawValue, ModbusValueType valueType, bool expectedNotAvailable)
    {
        Assert.Equal(expectedNotAvailable, NotAvailableValues.IsNotAvailable((decimal)rawValue, valueType));
    }

    [Fact]
    public void ProductionThatIsNotAvailableIsZero()
    {
        Assert.Equal(0, NotAvailableValues.ValueForNotAvailable(ValueUsage.InverterPower, 30775));
    }

    [Theory]
    [InlineData(ValueUsage.GridPower)]
    [InlineData(ValueUsage.HomeBatteryPower)]
    [InlineData(ValueUsage.HomeBatterySoc)]
    [InlineData(null)]
    public void AnyOtherValueThatIsNotAvailableCanNotBeUsed(ValueUsage? usage)
    {
        var exception = Assert.Throws<System.IO.InvalidDataException>(() => NotAvailableValues.ValueForNotAvailable(usage, 31249));
        Assert.Contains("31249", exception.Message);
    }
}
