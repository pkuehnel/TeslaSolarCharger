using Autofac;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeslaSolarCharger.Server.Services.ApiServices;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Contracts;
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

    public PvValuePublishingTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
        Mock.Mock<IDateTimeProvider>().Setup(d => d.DateTimeOffSetUtcNow()).Returns(CurrentFakeDate);
        Mock.Mock<ILoadPointManagementService>()
            .Setup(s => s.GetLoadPointsWithChargingDetails())
            .ReturnsAsync(new List<DtoLoadPointWithCurrentChargingValues>());
    }

    private static DtoPvSourceValue Source(ValueUsage usage, decimal value, int sourceId = 1) => new()
    {
        ConfigurationType = ConfigurationType.TemplateValue,
        SourceId = sourceId,
        UsedFor = usage,
        Value = value,
    };

    private IndexService CreateIndexService() => Mock.Create<IndexService>(new TypedParameter(typeof(ISettings), _settings));

    private TeslaSolarCharger.Server.Services.PvValueService CreatePvValueService(IEnumerable<IDecimalValueHandlingService> handlingServices) =>
        Mock.Create<TeslaSolarCharger.Server.Services.PvValueService>(
            new TypedParameter(typeof(ISettings), _settings),
            new TypedParameter(typeof(IIndexService), CreateIndexService()),
            new TypedParameter(typeof(IEnumerable<IDecimalValueHandlingService>), handlingServices));

    /// <summary>Creates the service with one value handling service per list, each delivering those device values.</summary>
    private TeslaSolarCharger.Server.Services.PvValueService CreatePvValueService(params List<DtoPvSourceValue>[] valuesPerHandlingService)
    {
        var handlingServices = valuesPerHandlingService.Select(values =>
        {
            var handlingService = new Mock<IDecimalValueHandlingService>();
            handlingService
                .Setup(s => s.GetSourceValues(It.IsAny<HashSet<ValueUsage>>(), It.IsAny<bool>()))
                .Returns(values);
            return handlingService.Object;
        }).ToList();
        return CreatePvValueService(handlingServices);
    }

    [Fact]
    public async Task TheChargingLogicWorksWithTheTotalsOfEveryDevice()
    {
        var pvValueService = CreatePvValueService(
            [Source(ValueUsage.InverterPower, 3000, sourceId: 1), Source(ValueUsage.HomeBatteryPower, 1500, sourceId: 1),],
            [Source(ValueUsage.InverterPower, 500, sourceId: 2), Source(ValueUsage.GridPower, -200, sourceId: 2),]);

        await pvValueService.UpdatePvValues();

        Assert.Equal(3500, _settings.InverterPower);
        Assert.Equal(-200, _settings.Overage);
        Assert.Equal(1500, _settings.HomeBatteryPower);
        //No device reads a state of charge, which must stay unknown rather than become zero.
        Assert.Null(_settings.HomeBatterySoc);
        Assert.Equal(4, _settings.PvSourceValues.Count);
        Assert.Equal(CurrentFakeDate, _settings.LastPvValueUpdate);
    }

    [Fact]
    public async Task OnlySolarBatteryAndGridReadingsWithoutErrorsAreAskedFor()
    {
        HashSet<ValueUsage>? askedUsages = null;
        bool? skippedErrors = null;
        var handlingService = new Mock<IDecimalValueHandlingService>();
        handlingService
            .Setup(s => s.GetSourceValues(It.IsAny<HashSet<ValueUsage>>(), It.IsAny<bool>()))
            .Callback<HashSet<ValueUsage>, bool>((usages, skip) => (askedUsages, skippedErrors) = (usages, skip))
            .Returns(new List<DtoPvSourceValue>());
        var pvValueService = CreatePvValueService(new[] { handlingService.Object, });

        await pvValueService.UpdatePvValues();

        Assert.NotNull(askedUsages);
        Assert.True(askedUsages.SetEquals(Enum.GetValues<ValueUsage>()));
        Assert.True(skippedErrors);
    }

    [Fact]
    public async Task WithoutAnyDeviceEveryTotalIsUnknown()
    {
        _settings.InverterPower = 1000;
        _settings.Overage = 100;
        _settings.HomeBatteryPower = 50;
        _settings.HomeBatterySoc = 40;
        var pvValueService = CreatePvValueService(new List<DtoPvSourceValue>());

        await pvValueService.UpdatePvValues();

        Assert.Null(_settings.InverterPower);
        Assert.Null(_settings.Overage);
        Assert.Null(_settings.HomeBatteryPower);
        Assert.Null(_settings.HomeBatterySoc);
        Assert.Empty(_settings.PvSourceValues);
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
        var pvValueService = CreatePvValueService([Source(ValueUsage.HomeBatteryPower, 1500),]);

        await pvValueService.UpdatePvValues();

        Assert.NotNull(sentValues);
        Assert.Equal(1500, Assert.Single(sentValues.SourceValues).Value);
        Assert.Equal(1500, sentValues.HomeBatteryPower);
        Mock.Mock<IAppStateNotifier>().Verify(n => n.NotifyStateUpdateAsync(update), Times.Once);
    }

    [Fact]
    public async Task NothingIsSentWhenNothingChanged()
    {
        Mock.Mock<IChangeTrackingService>()
            .Setup(s => s.DetectChanges(DataTypeConstants.PvValues, null, It.IsAny<DtoPvValues>()))
            .Returns((StateUpdateDto?)null);
        var pvValueService = CreatePvValueService([Source(ValueUsage.GridPower, 100),]);

        await pvValueService.UpdatePvValues();

        Mock.Mock<IAppStateNotifier>().Verify(n => n.NotifyStateUpdateAsync(It.IsAny<StateUpdateDto>()), Times.Never);
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
        var pvValueService = CreatePvValueService([Source(ValueUsage.GridPower, 99999),]);

        await pvValueService.UpdatePvValues();

        Assert.Equal(inverterPower, _settings.InverterPower);
        Assert.Equal(gridPower, _settings.Overage);
        Assert.Equal(homeBatteryPower, _settings.HomeBatteryPower);
        Assert.Equal(homeBatterySoc, _settings.HomeBatterySoc);
        Assert.All(_settings.PvSourceValues, v => Assert.Equal(ConfigurationType.FakeSolarValue, v.ConfigurationType));
        Assert.Equal(demoCase + 1, _settings.LastPvDemoCase);
    }

    [Fact]
    public async Task ThePagesGetTheTotalsFromTheStoredDeviceValues()
    {
        _settings.PvSourceValues = [Source(ValueUsage.GridPower, 400), Source(ValueUsage.InverterPower, 1200),];
        _settings.LastPvValueUpdate = CurrentFakeDate;
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.PowerBuffer()).Returns(250);
        Mock.Mock<ILoadPointManagementService>()
            .Setup(s => s.GetLoadPointsWithChargingDetails())
            .ReturnsAsync([new DtoLoadPointWithCurrentChargingValues { ChargingPower = 700, }, new DtoLoadPointWithCurrentChargingValues { ChargingPower = 300, },]);
        var indexService = CreateIndexService();

        var pvValues = await indexService.GetPvValues();

        Assert.Same(_settings.PvSourceValues, pvValues.SourceValues);
        Assert.Equal(400, pvValues.GridPower);
        Assert.Equal(1200, pvValues.InverterPower);
        Assert.Equal(250, pvValues.PowerBuffer);
        Assert.Equal(1000, pvValues.CarCombinedChargingPowerAtHome);
        Assert.Equal(CurrentFakeDate, pvValues.LastUpdated);
    }

    [Theory]
    //Without solar or grid values there is nothing a buffer could be kept from.
    [InlineData(null, null)]
    [InlineData(ValueUsage.HomeBatteryPower, null)]
    [InlineData(ValueUsage.GridPower, 250)]
    [InlineData(ValueUsage.InverterPower, 250)]
    public async Task ThePowerBufferIsOnlyShownWithSolarOrGridValues(ValueUsage? measuredUsage, int? expectedPowerBuffer)
    {
        _settings.PvSourceValues = measuredUsage == null ? [] : [Source(measuredUsage.Value, 100),];
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.PowerBuffer()).Returns(250);
        var indexService = CreateIndexService();

        var pvValues = await indexService.GetPvValues();

        Assert.Equal(expectedPowerBuffer, pvValues.PowerBuffer);
    }
}
