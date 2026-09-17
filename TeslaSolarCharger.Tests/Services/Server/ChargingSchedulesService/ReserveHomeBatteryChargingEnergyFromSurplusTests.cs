using System;
using System.Collections.Generic;
using Moq;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingSchedulesService;

public class ReserveHomeBatteryChargingEnergyFromSurplusTests : TestBase
{
    private const int HomeBatteryUsableEnergy = 30_000;
    private const int HomeBatteryChargingPower = 3_000;

    public ReserveHomeBatteryChargingEnergyFromSurplusTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    private void SetupHomeBattery(int? usableEnergy = HomeBatteryUsableEnergy, int? minSoc = 80, int? soc = 65,
        int? chargingPower = HomeBatteryChargingPower)
    {
        var configurationWrapperMock = Mock.Mock<IConfigurationWrapper>();
        configurationWrapperMock.Setup(c => c.HomeBatteryUsableEnergy()).Returns(usableEnergy);
        configurationWrapperMock.Setup(c => c.HomeBatteryMinSoc()).Returns(minSoc);
        configurationWrapperMock.Setup(c => c.HomeBatteryChargingPower()).Returns(chargingPower);
        Mock.Mock<ISettings>().Setup(s => s.HomeBatterySoc).Returns(soc);
    }

    /// <summary>
    /// Battery is 15% below min SoC (=4500Wh deficit). The reservation must be applied to the chronologically first
    /// slices (even when the dictionary is not ordered) and must be capped at the battery charging power per slice.
    /// </summary>
    [Fact]
    public void ReservesDeficitChronologically_CappedAtChargingPower()
    {
        SetupHomeBattery();
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        //Intentionally inserted out of order to make sure the reservation orders slices by time
        var surplusSlices = new Dictionary<DateTimeOffset, int>
        {
            { CurrentFakeDate.AddHours(1), 5_000 },
            { CurrentFakeDate, 5_000 },
        };

        var result = service.ReserveHomeBatteryChargingEnergyFromSurplus(surplusSlices);

        Assert.Equal(2_000, result[CurrentFakeDate]);
        Assert.Equal(3_500, result[CurrentFakeDate.AddHours(1)]);
    }

    [Fact]
    public void ReservesEverything_WhenDeficitExceedsSurplus()
    {
        //Deficit: (100 - 65) * 30000 / 100 = 10500Wh, charging power cap 3000Wh per slice
        SetupHomeBattery(minSoc: 100);
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var surplusSlices = new Dictionary<DateTimeOffset, int>
        {
            { CurrentFakeDate, 2_000 },
            { CurrentFakeDate.AddHours(1), 2_000 },
        };

        var result = service.ReserveHomeBatteryChargingEnergyFromSurplus(surplusSlices);

        Assert.Equal(0, result[CurrentFakeDate]);
        Assert.Equal(0, result[CurrentFakeDate.AddHours(1)]);
    }

    [Fact]
    public void NegativeSlices_AreUntouched_AndDoNotConsumeDeficit()
    {
        //Deficit 4500Wh
        SetupHomeBattery();
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var surplusSlices = new Dictionary<DateTimeOffset, int>
        {
            { CurrentFakeDate, -1_000 },
            { CurrentFakeDate.AddHours(1), 4_000 },
            { CurrentFakeDate.AddHours(2), 4_000 },
        };

        var result = service.ReserveHomeBatteryChargingEnergyFromSurplus(surplusSlices);

        Assert.Equal(-1_000, result[CurrentFakeDate]);
        Assert.Equal(1_000, result[CurrentFakeDate.AddHours(1)]);
        Assert.Equal(2_500, result[CurrentFakeDate.AddHours(2)]);
    }

    [Fact]
    public void PassesThrough_WhenBatteryNotBelowMinSoc()
    {
        SetupHomeBattery(minSoc: 65, soc: 65);
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var surplusSlices = new Dictionary<DateTimeOffset, int> { { CurrentFakeDate, 5_000 }, };

        var result = service.ReserveHomeBatteryChargingEnergyFromSurplus(surplusSlices);

        Assert.Equal(5_000, result[CurrentFakeDate]);
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void PassesThrough_WhenRequiredValueIsUnknown(bool unknownUsableEnergy, bool unknownMinSoc, bool unknownSoc,
        bool unknownChargingPower)
    {
        SetupHomeBattery(
            usableEnergy: unknownUsableEnergy ? null : HomeBatteryUsableEnergy,
            minSoc: unknownMinSoc ? null : 80,
            soc: unknownSoc ? null : 65,
            chargingPower: unknownChargingPower ? null : HomeBatteryChargingPower);
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var surplusSlices = new Dictionary<DateTimeOffset, int> { { CurrentFakeDate, 5_000 }, };

        var result = service.ReserveHomeBatteryChargingEnergyFromSurplus(surplusSlices);

        Assert.Equal(5_000, result[CurrentFakeDate]);
    }

    /// <summary>
    /// The surplus slice dictionary is shared between loadpoints and other consumers like the predicted house
    /// consumption calculation, so the reservation must never modify it.
    /// </summary>
    [Fact]
    public void DoesNotMutateInputDictionary()
    {
        SetupHomeBattery();
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var surplusSlices = new Dictionary<DateTimeOffset, int>
        {
            { CurrentFakeDate, 5_000 },
            { CurrentFakeDate.AddHours(1), 5_000 },
        };

        var result = service.ReserveHomeBatteryChargingEnergyFromSurplus(surplusSlices);

        Assert.NotEqual(5_000, result[CurrentFakeDate]);
        Assert.Equal(5_000, surplusSlices[CurrentFakeDate]);
        Assert.Equal(5_000, surplusSlices[CurrentFakeDate.AddHours(1)]);
    }
}
