using System;
using System.Collections.Concurrent;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.TargetChargingValueCalculationService;

public class GetTargetValueTests : TestBase
{
    public GetTargetValueTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    private void SetupOcppConnectorState(int connectorId, DtoOcppConnectorState state)
    {
        var settingsMock = Mock.Mock<ISettings>();
        var dictionary = new ConcurrentDictionary<int, DtoOcppConnectorState>();
        dictionary.TryAdd(connectorId, state);
        settingsMock.Setup(s => s.OcppConnectorStates).Returns(dictionary);
    }

    [Fact]
    public void GetTargetValue_NotPluggedIn_ReturnsNull()
    {
        // Arrange
        var sut = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();
        var constraintValues = new ConstraintValues();
        var loadPoint = new DtoLoadPointOverview { IsPluggedIn = false, IsHome = true };

        // Act
        var result = sut.GetTargetValue(constraintValues, loadPoint, 1000, false, CurrentFakeDate);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetTargetValue_NotAtHome_ReturnsNull()
    {
        // Arrange
        var sut = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();
        var constraintValues = new ConstraintValues();
        var loadPoint = new DtoLoadPointOverview { IsPluggedIn = true, IsHome = false };

        // Act
        var result = sut.GetTargetValue(constraintValues, loadPoint, 1000, false, CurrentFakeDate);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(5, 6, true, true)]   // Max < Min, IsCharging = true -> StopCharging = true
    [InlineData(5, 6, false, false)] // Max < Min, IsCharging = false -> Returns null
    public void GetTargetValue_MaxCurrentLessThanMinCurrent_HandlesCorrectly(int maxCurrent, int minCurrent, bool isCharging, bool expectedStopCharging)
    {
        // Arrange
        var sut = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();
        var constraintValues = new ConstraintValues
        {
            MaxCurrent = maxCurrent,
            MinCurrent = minCurrent,
            IsCharging = isCharging
        };
        var loadPoint = new DtoLoadPointOverview
        {
            IsPluggedIn = true,
            IsHome = true,
            CarId = 1,
            ChargingConnectorId = 1
        };

        SetupOcppConnectorState(1, new DtoOcppConnectorState());

        // Act
        var result = sut.GetTargetValue(constraintValues, loadPoint, 1000, false, CurrentFakeDate);

        // Assert
        if (expectedStopCharging)
        {
            Assert.NotNull(result);
            Assert.True(result.StopCharging);
        }
        else
        {
            Assert.Null(result);
        }
    }

    [Fact]
    public void GetTargetValue_ChargeModeManual_ReturnsNull()
    {
        // Arrange
        var sut = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();
        var constraintValues = new ConstraintValues
        {
            MaxCurrent = 16,
            MinCurrent = 6,
            ChargeMode = ChargeModeV2.Manual
        };
        var loadPoint = new DtoLoadPointOverview { IsPluggedIn = true, IsHome = true };

        // Act
        var result = sut.GetTargetValue(constraintValues, loadPoint, 1000, false, CurrentFakeDate);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(ChargeModeV2.Off, 50, 80, false)] // Off -> null
    [InlineData(ChargeModeV2.Auto, 90, 80, false)] // Solar, Soc > MaxSoc, !ignoreTimers -> null
    [InlineData(ChargeModeV2.Auto, 90, 80, true)]  // Solar, Soc > MaxSoc, ignoreTimers -> Continue (Not null immediately)
    public void GetTargetValue_ChargeModeOffOrMaxSocReached(ChargeModeV2 chargeMode, int soc, int maxSoc, bool ignoreTimers)
    {
        // Arrange
        var sut = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();
        var constraintValues = new ConstraintValues
        {
            MaxCurrent = 16,
            MinCurrent = 6,
            ChargeMode = chargeMode,
            Soc = soc,
            MaxSoc = maxSoc,
            IsCharging = false, // Assuming not charging
            // Set other required values to avoid early exit if we expect to continue
            ChargeStartAllowed = true,
            MinPhases = 1,
            MaxPhases = 3
        };
        var loadPoint = new DtoLoadPointOverview
        {
            IsPluggedIn = true,
            IsHome = true,
            CarId = 1,
            ChargingConnectorId = 1,
            ActualPhases = 1,
            EstimatedVoltageWhileCharging = 230
        };

        SetupOcppConnectorState(1, new DtoOcppConnectorState());

        // Act
        var result = sut.GetTargetValue(constraintValues, loadPoint, 1000, ignoreTimers, CurrentFakeDate);

        // Assert
        if (chargeMode == ChargeModeV2.Off || (!ignoreTimers && soc > maxSoc))
        {
            Assert.Null(result);
        }
        else
        {
            // If it continues, it should return a value (calculated current)
            Assert.NotNull(result);
            Assert.False(result.StopCharging);
        }
    }

    [Fact]
    public void GetTargetValue_CalculatesCurrentCorrectly()
    {
        // Arrange
        var sut = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();
        var constraintValues = new ConstraintValues
        {
            MaxCurrent = 16,
            MinCurrent = 6,
            ChargeMode = ChargeModeV2.Auto,
            Soc = 50,
            MaxSoc = 80,
            IsCharging = true,
            MinPhases = 1,
            MaxPhases = 3,
            ChargeStartAllowed = true
        };
        var loadPoint = new DtoLoadPointOverview
        {
            IsPluggedIn = true,
            IsHome = true,
            CarId = 1,
            ChargingConnectorId = 1,
            ActualPhases = 3,
            EstimatedVoltageWhileCharging = 230,
            ManageChargingPowerByCar = false
        };

        SetupOcppConnectorState(1, new DtoOcppConnectorState());

        // Power to set: 6900W
        // Voltage: 230V, Phases: 3
        // Expected Current = 6900 / (230 * 3) = 10A

        int powerToSet = 6900;

        // Act
        var result = sut.GetTargetValue(constraintValues, loadPoint, powerToSet, false, CurrentFakeDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10m, result.TargetCurrent.Value, 2);
    }

    [Fact]
    public void GetTargetValue_ClampsCurrentToMinMax()
    {
        // Arrange
        var sut = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();
        var minCurrent = 6;
        var maxCurrent = 16;

        var constraintValues = new ConstraintValues
        {
            MaxCurrent = maxCurrent,
            MinCurrent = minCurrent,
            ChargeMode = ChargeModeV2.Auto,
            Soc = 50,
            MaxSoc = 80,
            IsCharging = true,
            MinPhases = 1,
            MaxPhases = 3,
        };
        var loadPoint = new DtoLoadPointOverview
        {
            IsPluggedIn = true,
            IsHome = true,
            CarId = 1,
            ChargingConnectorId = 1,
            ActualPhases = 3,
            EstimatedVoltageWhileCharging = 230
        };

        SetupOcppConnectorState(1, new DtoOcppConnectorState());

        // Case 1: Too Low
        // 1000W / (230*3) = 1.45A -> Should clamp to 6A
        var resultLow = sut.GetTargetValue(constraintValues, loadPoint, 1000, false, CurrentFakeDate);
        Assert.NotNull(resultLow);
        Assert.Equal(minCurrent, resultLow.TargetCurrent);

        // Case 2: Too High
        // 20000W / (230*3) = 28.9A -> Should clamp to 16A
        var resultHigh = sut.GetTargetValue(constraintValues, loadPoint, 20000, false, CurrentFakeDate);
        Assert.NotNull(resultHigh);
        Assert.Equal(maxCurrent, resultHigh.TargetCurrent);
    }

    [Fact]
    public void GetTargetValue_CarSocLimitReached_ReturnsNull()
    {
         // Arrange
        var sut = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();
        var constraintValues = new ConstraintValues
        {
            MaxCurrent = 16,
            MinCurrent = 6,
            ChargeMode = ChargeModeV2.Auto,
            Soc = 80,
            CarSocLimit = 80, // Same as SOC, difference is 0. Constant MinimumSocDifference is likely > 0
            IsCharging = false,
            ChargeStartAllowed = true,
            MinPhases = 1,
            MaxPhases = 3
        };
        var loadPoint = new DtoLoadPointOverview
        {
            IsPluggedIn = true,
            IsHome = true,
            CarId = 1,
            ChargingConnectorId = 1,
            ActualPhases = 1,
            EstimatedVoltageWhileCharging = 230
        };

        SetupOcppConnectorState(1, new DtoOcppConnectorState());

        // Act
        var result = sut.GetTargetValue(constraintValues, loadPoint, 1000, false, CurrentFakeDate);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetTargetValue_UseCarToManageChargingSpeed_ReturnsValidResult()
    {
         // Arrange
        var sut = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();
        var constraintValues = new ConstraintValues
        {
            MaxCurrent = 16,
            MinCurrent = 6,
            ChargeMode = ChargeModeV2.Auto,
            Soc = 50,
            IsCharging = true,
            MinPhases = 1,
            MaxPhases = 3
        };
        var loadPoint = new DtoLoadPointOverview
        {
            IsPluggedIn = true,
            IsHome = true,
            CarId = 1,
            ChargingConnectorId = 1,
            ActualPhases = 1,
            EstimatedVoltageWhileCharging = 230,
            ManageChargingPowerByCar = true
        };

        SetupOcppConnectorState(1, new DtoOcppConnectorState());

        // Act
        var result = sut.GetTargetValue(constraintValues, loadPoint, 1000, false, CurrentFakeDate);

        // Assert
        Assert.NotNull(result);
        // We just verify it returns a valid result.
        Assert.NotNull(result.TargetCurrent);
    }
}
