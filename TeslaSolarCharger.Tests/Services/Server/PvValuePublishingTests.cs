using Autofac;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeslaSolarCharger.Server.Services.ApiServices;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Fake;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Fake.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Server.SignalR.Notifiers.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.SignalRClients;
using TeslaSolarCharger.SharedModel.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

/// <summary>
/// The solar and battery values from every device, the totals the charging logic takes from them and what the pages
/// are sent.
/// </summary>
public class PvValuePublishingTests : TestBase
{
    private readonly Settings _settings = new();
    private List<DtoPvSourceValue> _deviceValues = new();

    public PvValuePublishingTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
        Mock.Mock<IDateTimeProvider>().Setup(d => d.DateTimeOffSetUtcNow()).Returns(CurrentFakeDate);
        Mock.Mock<ILoadPointManagementService>()
            .Setup(s => s.GetLoadPointsWithChargingDetails())
            .ReturnsAsync(new List<DtoLoadPointWithCurrentChargingValues>());
        Mock.Mock<IGenericValueService>()
            .Setup(s => s.GetSourceValues(It.IsAny<bool>()))
            .Returns(() => _deviceValues);
    }

    private DtoPvSourceValue Source(ValueUsage usage, decimal value, int sourceId = 1,
        ConfigurationType configurationType = ConfigurationType.TemplateValue) => new()
    {
        ConfigurationType = configurationType,
        SourceId = sourceId,
        UsedFor = usage,
        Value = new(CurrentFakeDate, value),
    };

    private IndexService CreateIndexService(IGenericValueService? genericValueService = null) => Mock.Create<IndexService>(
        new TypedParameter(typeof(ISettings), _settings),
        new TypedParameter(typeof(IGenericValueService), genericValueService ?? Mock.Mock<IGenericValueService>().Object));

    private TeslaSolarCharger.Server.Services.PvValueService CreatePvValueService(IGenericValueService? genericValueService = null,
        IFakeSolarValueHandlingService? fakeSolarValueHandlingService = null) =>
        Mock.Create<TeslaSolarCharger.Server.Services.PvValueService>(
            new TypedParameter(typeof(ISettings), _settings),
            new TypedParameter(typeof(IIndexService), CreateIndexService(genericValueService)),
            new TypedParameter(typeof(IFakeSolarValueHandlingService),
                fakeSolarValueHandlingService ?? Mock.Mock<IFakeSolarValueHandlingService>().Object));

    /// <summary>
    /// The value handling as the app wires it: a real device reading <paramref name="realGridPower"/> next to the fake
    /// device, both reached through the one generic value service.
    /// </summary>
    private (IGenericValueService GenericValueService, FakeSolarValueHandlingService FakeSolarValueHandlingService) CreateRealValueHandling(
        decimal realGridPower)
    {
        var realDevice = new Mock<IGenericValue<decimal>>();
        realDevice.Setup(v => v.SourceValueKey).Returns(new SourceValueKey(1, ConfigurationType.TemplateValue));
        realDevice.Setup(v => v.HistoricValues).Returns(new Dictionary<ValueKey, DtoHistoricValue<decimal>>
        {
            { new ValueKey(ValueUsage.GridPower, null, 1), new DtoHistoricValue<decimal>(CurrentFakeDate, realGridPower, 1) },
        });
        var realHandlingService = new Mock<IDecimalValueHandlingService>();
        realHandlingService.Setup(s => s.GetSnapshot()).Returns([realDevice.Object,]);
        var fakeSolarValueHandlingService = new FakeSolarValueHandlingService(new Mock<IServiceScopeFactory>().Object);
        var genericValueService = new GenericValueService(NullLogger<GenericValueService>.Instance,
            [realHandlingService.Object, fakeSolarValueHandlingService,]);
        return (genericValueService, fakeSolarValueHandlingService);
    }

    [Fact]
    public async Task TheChargingLogicWorksWithTheTotalsOfEveryDevice()
    {
        _deviceValues =
        [
            Source(ValueUsage.InverterPower, 3000, sourceId: 1), Source(ValueUsage.HomeBatteryPower, 1500, sourceId: 1),
            Source(ValueUsage.InverterPower, 500, sourceId: 2), Source(ValueUsage.GridPower, -200, sourceId: 2),
        ];

        await CreatePvValueService().UpdatePvValues();

        Assert.Equal(3500, _settings.InverterPower);
        Assert.Equal(-200, _settings.Overage);
        Assert.Equal(1500, _settings.HomeBatteryPower);
        //No device reads a state of charge, which must stay unknown rather than become zero.
        Assert.Null(_settings.HomeBatterySoc);
        Assert.Equal(CurrentFakeDate, _settings.LastPvValueUpdate);
    }

    [Fact]
    public async Task OnlyReadingsWithoutErrorsAreUsed()
    {
        await CreatePvValueService().UpdatePvValues();

        Mock.Mock<IGenericValueService>().Verify(s => s.GetSourceValues(true), Times.Once);
        Mock.Mock<IGenericValueService>().Verify(s => s.GetSourceValues(false), Times.Never);
    }

    [Fact]
    public async Task WithoutAnyDeviceEveryTotalIsUnknown()
    {
        _settings.InverterPower = 1000;
        _settings.Overage = 100;
        _settings.HomeBatteryPower = 50;
        _settings.HomeBatterySoc = 40;

        await CreatePvValueService().UpdatePvValues();

        Assert.Null(_settings.InverterPower);
        Assert.Null(_settings.Overage);
        Assert.Null(_settings.HomeBatteryPower);
        Assert.Null(_settings.HomeBatterySoc);
    }

    [Fact]
    public async Task ThePagesAreSentTheValuesPerDevice()
    {
        DtoPvValues? sentValues = null;
        var update = new StateUpdateDto { DataType = DataTypeConstants.PvValues, };
        Mock.Mock<IChangeTrackingService>()
            .Setup(s => s.DetectChanges(DataTypeConstants.PvValues, null, It.IsAny<DtoPvValues>()))
            .Callback<string, string?, DtoPvValues>((_, _, pvValues) => sentValues = pvValues)
            .Returns(update);
        _deviceValues = [Source(ValueUsage.HomeBatteryPower, 1500),];

        await CreatePvValueService().UpdatePvValues();

        Assert.NotNull(sentValues);
        Assert.Equal(1500, Assert.Single(sentValues.SourceValues).Value.Value);
        Assert.Equal(1500, sentValues.HomeBatteryPower);
        Mock.Mock<IAppStateNotifier>().Verify(n => n.NotifyStateUpdateAsync(update), Times.Once);
    }

    [Fact]
    public async Task NothingIsSentWhenNothingChanged()
    {
        Mock.Mock<IChangeTrackingService>()
            .Setup(s => s.DetectChanges(DataTypeConstants.PvValues, null, It.IsAny<DtoPvValues>()))
            .Returns((StateUpdateDto?)null);
        _deviceValues = [Source(ValueUsage.GridPower, 100),];

        await CreatePvValueService().UpdatePvValues();

        Mock.Mock<IAppStateNotifier>().Verify(n => n.NotifyStateUpdateAsync(It.IsAny<StateUpdateDto>()), Times.Never);
    }

    [Fact]
    public async Task WithoutFakeValuesTheFakeDeviceIsLeftEmpty()
    {
        await CreatePvValueService().UpdatePvValues();

        Mock.Mock<IFakeSolarValueHandlingService>().Verify(
            s => s.SetValues(It.IsAny<DateTimeOffset>(), It.IsAny<IReadOnlyDictionary<ValueUsage, int?>>()), Times.Never);
        Assert.Equal(0, _settings.LastPvDemoCase);
    }

    [Theory]
    //Case 12 has every measurement, case 1 only the grid, case 0 none at all.
    [InlineData(12, 1000, 300, 500, 20)]
    [InlineData(1, null, 200, null, null)]
    [InlineData(0, null, null, null, null)]
    public async Task FakeValuesReachTheChargingLogicLikeRealOnes(int demoCase, int? inverterPower, int? gridPower,
        int? homeBatteryPower, int? homeBatterySoc)
    {
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.ShouldUseFakeSolarValues()).Returns(true);
        _settings.LastPvDemoCase = demoCase;
        //A real device is still configured, but fake values replace it entirely.
        var (genericValueService, fakeSolarValueHandlingService) = CreateRealValueHandling(99999);
        DtoPvValues? sentValues = null;
        Mock.Mock<IChangeTrackingService>()
            .Setup(s => s.DetectChanges(DataTypeConstants.PvValues, null, It.IsAny<DtoPvValues>()))
            .Callback<string, string?, DtoPvValues>((_, _, pvValues) => sentValues = pvValues);

        await CreatePvValueService(genericValueService, fakeSolarValueHandlingService).UpdatePvValues();

        Assert.Equal(inverterPower, _settings.InverterPower);
        Assert.Equal(gridPower, _settings.Overage);
        Assert.Equal(homeBatteryPower, _settings.HomeBatteryPower);
        Assert.Equal(homeBatterySoc, _settings.HomeBatterySoc);
        Assert.NotNull(sentValues);
        Assert.All(sentValues.SourceValues, v =>
        {
            Assert.Equal(ConfigurationType.FakeSolarValue, v.ConfigurationType);
            Assert.Equal(CurrentFakeDate, v.Value.Timestamp);
        });
        Assert.Equal(demoCase + 1, _settings.LastPvDemoCase);
    }

    [Fact]
    public async Task AFakeMeasurementDroppedByTheNextCaseDoesNotStayBehind()
    {
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.ShouldUseFakeSolarValues()).Returns(true);
        var (genericValueService, fakeSolarValueHandlingService) = CreateRealValueHandling(99999);
        var pvValueService = CreatePvValueService(genericValueService, fakeSolarValueHandlingService);
        //Case 12 has every measurement, case 0 (reached again at 16) none at all.
        _settings.LastPvDemoCase = 12;
        await pvValueService.UpdatePvValues();
        Assert.Equal(500, _settings.HomeBatteryPower);

        _settings.LastPvDemoCase = 16;
        await pvValueService.UpdatePvValues();

        Assert.Null(_settings.InverterPower);
        Assert.Null(_settings.Overage);
        Assert.Null(_settings.HomeBatteryPower);
        Assert.Null(_settings.HomeBatterySoc);
    }

    [Fact]
    public async Task ThePagesGetTheCurrentValuesOfEveryDevice()
    {
        _deviceValues = [Source(ValueUsage.GridPower, 400), Source(ValueUsage.InverterPower, 1200),];
        _settings.LastPvValueUpdate = CurrentFakeDate;
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.PowerBuffer()).Returns(250);
        Mock.Mock<ILoadPointManagementService>()
            .Setup(s => s.GetLoadPointsWithChargingDetails())
            .ReturnsAsync([new DtoLoadPointWithCurrentChargingValues { ChargingPower = 700, }, new DtoLoadPointWithCurrentChargingValues { ChargingPower = 300, },]);

        var pvValues = await CreateIndexService().GetPvValues();

        Assert.Equal(_deviceValues, pvValues.SourceValues);
        Assert.Equal(400, pvValues.GridPower);
        Assert.Equal(1200, pvValues.InverterPower);
        Assert.Equal(250, pvValues.PowerBuffer);
        Assert.Equal(1000, pvValues.CarCombinedChargingPowerAtHome);
        Assert.Equal(CurrentFakeDate, pvValues.LastUpdated);
        Mock.Mock<IGenericValueService>().Verify(s => s.GetSourceValues(true), Times.Once);
    }

    [Theory]
    [InlineData(true, 1000)]
    [InlineData(false, 1400)]
    public async Task FakeValuesReplaceTheRealDevicesOnlyWhileSwitchedOn(bool useFakeValues, int expectedInverterPower)
    {
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.ShouldUseFakeSolarValues()).Returns(useFakeValues);
        _deviceValues =
        [
            Source(ValueUsage.InverterPower, 400),
            Source(ValueUsage.InverterPower, 1000, sourceId: 0, configurationType: ConfigurationType.FakeSolarValue),
        ];

        var pvValues = await CreateIndexService().GetPvValues();

        Assert.Equal(expectedInverterPower, pvValues.InverterPower);
    }

    [Theory]
    //Without solar or grid values there is nothing a buffer could be kept from.
    [InlineData(null, null)]
    [InlineData(ValueUsage.HomeBatteryPower, null)]
    [InlineData(ValueUsage.GridPower, 250)]
    [InlineData(ValueUsage.InverterPower, 250)]
    public async Task ThePowerBufferIsOnlyShownWithSolarOrGridValues(ValueUsage? measuredUsage, int? expectedPowerBuffer)
    {
        _deviceValues = measuredUsage == null ? [] : [Source(measuredUsage.Value, 100),];
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.PowerBuffer()).Returns(250);

        var pvValues = await CreateIndexService().GetPvValues();

        Assert.Equal(expectedPowerBuffer, pvValues.PowerBuffer);
    }
}
