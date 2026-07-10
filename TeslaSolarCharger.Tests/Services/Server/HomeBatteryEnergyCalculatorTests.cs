using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class HomeBatteryEnergyCalculatorTests : TestBase
{
    public HomeBatteryEnergyCalculatorTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public void CalculateRequiredInitialStateOfChargePercent_NoDeficit_ReturnsMinSocAndNoBreach()
    {
        // Arrange
        var calculator = Mock.Create<HomeBatteryEnergyCalculator>();
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryChargingPower()).Returns(10000);

        var capacity = 10000; // 10kWh usable
        var minSoc = 5;

        // All positive surplus, no deficit
        var slices = new Dictionary<DateTimeOffset, int>
        {
            { new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero), 1000 },
            { new DateTimeOffset(2023, 2, 2, 10, 0, 0, TimeSpan.Zero), 1000 },
            { new DateTimeOffset(2023, 2, 2, 11, 0, 0, TimeSpan.Zero), 1000 },
        };
        var targetTime = new DateTimeOffset(2023, 2, 2, 10, 0, 0, TimeSpan.Zero);
        var buffer = 0;

        // Act
        var result = calculator.CalculateRequiredInitialStateOfChargePercent(
            slices, capacity, minSoc, minSoc, targetTime, buffer);

        // Assert
        Assert.Equal(minSoc, result.RequiredInitialSocPercent);
        Assert.Null(result.FirstBreachTime);
        Assert.Equal(0, result.AdditionalEnergyRequiredWh);
        Assert.Equal(targetTime, result.SelfSufficiencyTime);
    }

    [Fact]
    public void CalculateRequiredInitialStateOfChargePercent_SimpleDeficit_CalculatesBreachAndAdditionalEnergy()
    {
        // Arrange
        var calculator = Mock.Create<HomeBatteryEnergyCalculator>();
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryChargingPower()).Returns(10000);

        var capacity = 10000;
        var minSoc = 5; // 500 Wh min
        var targetSoc = 5;

        // One big deficit slice
        var slices = new Dictionary<DateTimeOffset, int>
        {
            { new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero), -2000 }, // consume 2000 net
        };
        var targetTime = new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero);
        var buffer = 0;

        // Starting at 500, apply -2000 -> -1500, missing = 500 - (-1500) = 2000

        // Act
        var result = calculator.CalculateRequiredInitialStateOfChargePercent(
            slices, capacity, minSoc, targetSoc, targetTime, buffer);

        // Assert
        Assert.Equal(25, result.RequiredInitialSocPercent); // 500 + 2000 = 2500 / 10000 = 25%
        Assert.Equal(new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero), result.FirstBreachTime);
        Assert.Equal(2000, result.AdditionalEnergyRequiredWh);
        Assert.Equal(targetTime, result.SelfSufficiencyTime);
    }

    [Fact]
    public void CalculateRequiredInitialStateOfChargePercent_WithBuffer_IncludesBufferInAdditionalAndSoc()
    {
        var calculator = Mock.Create<HomeBatteryEnergyCalculator>();
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryChargingPower()).Returns(10000);

        var capacity = 10000;
        var minSoc = 5;
        var slices = new Dictionary<DateTimeOffset, int>
        {
            { new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero), -1000 },
        };
        var targetTime = new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero);
        var buffer = 50; // factor 1.5

        // missing = 1000 (raw), final = 1500
        // required = (500 + 1500)/10000 *100 = 20%

        var result = calculator.CalculateRequiredInitialStateOfChargePercent(
            slices, capacity, minSoc, minSoc, targetTime, buffer);

        Assert.Equal(20, result.RequiredInitialSocPercent);
        Assert.Equal(1500, result.AdditionalEnergyRequiredWh);
        Assert.NotNull(result.FirstBreachTime);
    }

    [Fact]
    public void CalculateRequiredInitialStateOfChargePercent_MultipleSlices_FirstBreachIsEarliest()
    {
        var calculator = Mock.Create<HomeBatteryEnergyCalculator>();
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryChargingPower()).Returns(10000);

        var capacity = 10000;
        var minSoc = 5;
        var slices = new Dictionary<DateTimeOffset, int>
        {
            { new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero), 500 },   // no breach
            { new DateTimeOffset(2023, 2, 2, 10, 0, 0, TimeSpan.Zero), -800 }, // breach here
            { new DateTimeOffset(2023, 2, 2, 11, 0, 0, TimeSpan.Zero), -300 },
        };
        var targetTime = new DateTimeOffset(2023, 2, 2, 12, 0, 0, TimeSpan.Zero);
        var buffer = 0;

        var result = calculator.CalculateRequiredInitialStateOfChargePercent(
            slices, capacity, minSoc, minSoc, targetTime, buffer);

        Assert.Equal(new DateTimeOffset(2023, 2, 2, 10, 0, 0, TimeSpan.Zero), result.FirstBreachTime);
        // Starting 500
        // t9: 500+500=1000, missing=0
        // t10: 1000-800=200 , missing=500-200=300
        // t11: 200-300= -100 , missing=600
        // maxMissing=600
        Assert.Equal(600, result.AdditionalEnergyRequiredWh);
    }

    [Fact]
    public void CalculateRequiredInitialStateOfChargePercent_TargetNotReached_IncreasesMissing()
    {
        var calculator = Mock.Create<HomeBatteryEnergyCalculator>();
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryChargingPower()).Returns(10000);

        var capacity = 10000;
        var minSoc = 5;
        var targetSoc = 50; // want to reach 50% by targetTime

        var slices = new Dictionary<DateTimeOffset, int>
        {
            { new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero), 0 },
        };
        var targetTime = new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero);
        var buffer = 0;

        // Start at min 500, +0 -> 500 at targetTime
        // target 5000 > 500 => maxMissing at least 4500
        // required = (500 + 4500) /10000 *100 = 50%

        var result = calculator.CalculateRequiredInitialStateOfChargePercent(
            slices, capacity, minSoc, targetSoc, targetTime, buffer);

        Assert.Equal(50, result.RequiredInitialSocPercent);
    }

    [Fact]
    public void CalculateRequiredInitialStateOfChargePercent_ChargingPowerLimited_CapsAddition()
    {
        var calculator = Mock.Create<HomeBatteryEnergyCalculator>();
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryChargingPower()).Returns(500); // low charge rate

        var capacity = 10000;
        var minSoc = 5;
        var slices = new Dictionary<DateTimeOffset, int>
        {
            { new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero), 10000 }, // huge surplus, but capped at 500
        };
        var targetTime = new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero);
        var buffer = 0;

        // Starts at 500, +500 (capped) -> 1000
        // No missing created.

        var result = calculator.CalculateRequiredInitialStateOfChargePercent(
            slices, capacity, minSoc, 5, targetTime, buffer);

        Assert.Equal(5, result.RequiredInitialSocPercent);
        Assert.Equal(0, result.AdditionalEnergyRequiredWh);
    }

    [Fact]
    public async Task RefreshHomeBatteryMinSoc_StoresHoldAndChargeTargetsInSettings_FetchesPredictionOnlyOnce()
    {
        // Arrange
        var calculator = Mock.Create<HomeBatteryEnergyCalculator>();
        Mock.Mock<IDateTimeProvider>().Setup(d => d.DateTimeOffSetUtcNow()).Returns(CurrentFakeDate);
        Mock.Mock<ISettings>().SetupAllProperties();

        var nextSunset = new DateTimeOffset(2023, 2, 2, 17, 0, 0, TimeSpan.Zero);
        var nextSunrise = new DateTimeOffset(2023, 2, 3, 7, 0, 0, TimeSpan.Zero);
        Mock.Mock<ISunCalculator>()
            .Setup(s => s.NextSunset(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<DateTimeOffset>(), It.IsAny<int>()))
            .Returns(nextSunset);
        Mock.Mock<ISunCalculator>()
            .Setup(s => s.NextSunrise(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<DateTimeOffset>(), It.IsAny<int>()))
            .Returns(nextSunrise);

        var configurationWrapperMock = Mock.Mock<IConfigurationWrapper>();
        configurationWrapperMock.Setup(c => c.HomeBatteryUsableEnergy()).Returns(10000);
        configurationWrapperMock.Setup(c => c.HomeBatteryChargingPower()).Returns(10000);
        configurationWrapperMock.Setup(c => c.HomeBatteryMinDynamicMinSoc()).Returns(5);
        configurationWrapperMock.Setup(c => c.HomeBatteryMaxDynamicMinSoc()).Returns(95);
        configurationWrapperMock.Setup(c => c.HoldHomeBatteryChargeSocBufferInPercent()).Returns(50);
        configurationWrapperMock.Setup(c => c.ChargeHomeBatterySocBufferInPercent()).Returns(100);
        // DynamicHomeBatteryMinSoc and ForceFullHomeBatteryBySunset stay at their mock default (false)

        // Deficit before sunrise, first positive surplus one hour after sunrise
        var breachSlice = new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero);
        var expectedSelfSufficiencyTime = new DateTimeOffset(2023, 2, 3, 8, 0, 0, TimeSpan.Zero);
        var slices = new Dictionary<DateTimeOffset, int>
        {
            { breachSlice, -1000 },
            { expectedSelfSufficiencyTime, 2000 },
        };
        Mock.Mock<IEnergyDataService>()
            .Setup(e => e.GetPredictedSurplusPerSlice(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(slices);

        // Act
        await calculator.RefreshHomeBatteryMinSoc(CancellationToken.None);

        // Assert
        Mock.Mock<IEnergyDataService>().Verify(
            e => e.GetPredictedSurplusPerSlice(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Once);

        var settings = Mock.Mock<ISettings>().Object;
        // Floor 500 Wh, -1000 -> -500 => 1000 Wh missing at the breach slice
        var holdTarget = settings.HomeBatteryHoldTarget;
        Assert.NotNull(holdTarget);
        Assert.Equal(20, holdTarget.RequiredInitialSocPercent); // (500 + 1000 * 1.5) / 10000
        Assert.Equal(1500, holdTarget.AdditionalEnergyRequiredWh);
        Assert.Equal(breachSlice, holdTarget.FirstBreachTime);
        Assert.Equal(expectedSelfSufficiencyTime, holdTarget.SelfSufficiencyTime);
        Assert.Equal(CurrentFakeDate, holdTarget.CalculatedAt);

        var chargeTarget = settings.HomeBatteryChargeTarget;
        Assert.NotNull(chargeTarget);
        Assert.Equal(25, chargeTarget.RequiredInitialSocPercent); // (500 + 1000 * 2) / 10000
        Assert.Equal(2000, chargeTarget.AdditionalEnergyRequiredWh);
        Assert.Equal(breachSlice, chargeTarget.FirstBreachTime);
        Assert.Equal(expectedSelfSufficiencyTime, chargeTarget.SelfSufficiencyTime);
        Assert.Equal(CurrentFakeDate, chargeTarget.CalculatedAt);
    }
}
