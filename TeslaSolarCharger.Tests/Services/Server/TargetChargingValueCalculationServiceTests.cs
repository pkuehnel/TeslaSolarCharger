using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

public class TargetChargingValueCalculationServiceTests : TestBase
{
    public TargetChargingValueCalculationServiceTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    private DtoTimeStampedValue<T> CreateTimeStampedValue<T>(T value, DateTimeOffset lastChanged)
    {
        var dto = new DtoTimeStampedValue<T>(lastChanged, value);
        var prop = typeof(DtoTimeStampedValue<T>).GetProperty("LastChanged");
        prop?.SetValue(dto, lastChanged);
        return dto;
    }

    private DtoCar CreateDefaultDtoCar(int id)
    {
        return new DtoCar
        {
            Id = id,
            Name = "Test Car",
            ShouldStartCharging = CreateTimeStampedValue<bool?>(true, DateTimeOffset.UtcNow),
            ShouldStopCharging = CreateTimeStampedValue<bool?>(false, DateTimeOffset.UtcNow),
            IsCharging = CreateTimeStampedValue<bool?>(true, DateTimeOffset.UtcNow),
            ChargerPhases = CreateTimeStampedValue<int?>(3, DateTimeOffset.UtcNow),
            SoC = CreateTimeStampedValue<int?>(50, DateTimeOffset.UtcNow),
            SocLimit = CreateTimeStampedValue<int?>(80, DateTimeOffset.UtcNow),
            ChargeModeV2 = ChargeModeV2.Auto,
        };
    }

    private Car CreateDefaultCarEntity(int id, CarType carType = CarType.Tesla)
    {
        return new Car
        {
            Id = id,
            Name = "Test Car",
            MinimumAmpere = 6,
            MaximumAmpere = 16,
            ChargeMode = ChargeModeV2.Auto,
            MaximumSoc = 90,
            MinimumSoc = 20,
            CarType = carType,
            MaximumPhases = 3,
        };
    }

    /// <summary>
    /// Verifies that when a valid CarId is provided, the service retrieves the car configuration
    /// and correctly populates the ConstraintValues (Amps, SOC, Phases, etc.).
    /// </summary>
    [Fact]
    public async Task GetConstraintValues_CarIdProvided_ReturnsCorrectValues()
    {
        var carId = 1;
        Context.Cars.Add(CreateDefaultCarEntity(carId));
        await Context.SaveChangesAsync();

        var dtoCar = CreateDefaultDtoCar(carId);

        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar> { dtoCar });
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.TimespanUntilSwitchOn()).Returns(TimeSpan.FromMinutes(1));
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.TimespanUntilSwitchOff()).Returns(TimeSpan.FromMinutes(1));

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();

        var result = await service.GetConstraintValues(carId, null, true, DateTimeOffset.UtcNow, 32m, CancellationToken.None);

        Assert.Equal(16, result.MaxCurrent);
        Assert.Equal(6, result.MinCurrent);
        Assert.Equal(ChargeModeV2.Auto, result.ChargeMode);
        Assert.Equal(3, result.MinPhases);
        Assert.Equal(3, result.MaxPhases);
        Assert.Equal(50, result.Soc);
        Assert.Equal(80, result.CarSocLimit);
        Assert.True(result.IsCharging);
        Assert.False(result.PhaseReductionAllowed);
    }

    /// <summary>
    /// Verifies that for a Non-Tesla car, the Min/Max Phases are taken from the configuration
    /// rather than the dynamic 'ActualPhases' property (which is specific to Tesla).
    /// </summary>
    [Fact]
    public async Task GetConstraintValues_NonTeslaCar_UsesConfigPhases()
    {
        var carId = 2;
        var carEntity = CreateDefaultCarEntity(carId, CarType.Manual);
        carEntity.MaximumPhases = 2;
        Context.Cars.Add(carEntity);
        await Context.SaveChangesAsync();

        var dtoCar = CreateDefaultDtoCar(carId);

        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar> { dtoCar });
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.TimespanUntilSwitchOn()).Returns(TimeSpan.FromMinutes(1));
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.TimespanUntilSwitchOff()).Returns(TimeSpan.FromMinutes(1));

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();

        var result = await service.GetConstraintValues(carId, null, true, DateTimeOffset.UtcNow, 32m, CancellationToken.None);

        Assert.Equal(2, result.MaxPhases);
    }

    /// <summary>
    /// Verifies that when a ConnectorId is provided, the service retrieves the connector configuration
    /// and updates the ConstraintValues. Also checks that Phase Reduction is allowed when conditions are met
    /// (OnePhase supported, ThreePhase NOT supported or lost).
    /// </summary>
    [Fact]
    public async Task GetConstraintValues_ConnectorIdProvided_UpdatesValues()
    {
        var connectorId = 10;
        var connectorEntity = new OcppChargingStationConnector("Test Connector")
        {
            Id = connectorId,
            MinCurrent = 10,
            MaxCurrent = 20,
            ConnectedPhasesCount = 3,
            AutoSwitchBetween1And3PhasesEnabled = true,
            ChargeMode = ChargeModeV2.Auto,
            PhaseSwitchCoolDownTimeSeconds = 60,
        };
        Context.OcppChargingStationConnectors.Add(connectorEntity);
        await Context.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        var lastChanged = now.AddMinutes(-5);

        var ocppState = new DtoOcppConnectorState
        {
            IsCharging = CreateTimeStampedValue(true, now),
            IsCarFullyCharged = CreateTimeStampedValue<bool?>(false, now),
            ShouldStartCharging = CreateTimeStampedValue<bool?>(true, now),
            ShouldStopCharging = CreateTimeStampedValue<bool?>(false, now),
            CanHandlePowerOnOnePhase = CreateTimeStampedValue<bool?>(true, lastChanged),
            CanHandlePowerOnThreePhase = CreateTimeStampedValue<bool?>(false, lastChanged), // false for Reduction
        };

        var ocppStates = new ConcurrentDictionary<int, DtoOcppConnectorState>();
        ocppStates.TryAdd(connectorId, ocppState);

        Mock.Mock<ISettings>().Setup(s => s.OcppConnectorStates).Returns(ocppStates);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.TimespanUntilSwitchOn()).Returns(TimeSpan.FromMinutes(1));
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.TimespanUntilSwitchOff()).Returns(TimeSpan.FromMinutes(1));

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();

        // useCarToManageChargingSpeed = false
        var result = await service.GetConstraintValues(null, connectorId, false, now, 32m, CancellationToken.None);

        Assert.Equal(20, result.MaxCurrent);
        Assert.Equal(10, result.MinCurrent);
        Assert.Equal(3, result.MaxPhases);
        Assert.True(result.PhaseSwitchingEnabled);
        Assert.Equal(1, result.MinPhases);
        Assert.True(result.IsCharging);
        Assert.True(result.PhaseReductionAllowed);
    }

    /// <summary>
    /// Verifies that if 'useCarToManageChargingSpeed' is true, Phase Reduction is forcefully disabled
    /// regardless of connector state.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetConstraintValues_UseCarToManageChargingSpeed_OverridesPhaseReduction(bool useCar)
    {
        var connectorId = 11;
        var connectorEntity = new OcppChargingStationConnector("Test Connector")
        {
            Id = connectorId,
            MinCurrent = 6,
            MaxCurrent = 32,
            ConnectedPhasesCount = 3,
            AutoSwitchBetween1And3PhasesEnabled = true,
        };
        Context.OcppChargingStationConnectors.Add(connectorEntity);
        await Context.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        var lastChanged = now.AddMinutes(-5);

        var ocppState = new DtoOcppConnectorState
        {
            CanHandlePowerOnOnePhase = CreateTimeStampedValue<bool?>(true, lastChanged),
            CanHandlePowerOnThreePhase = CreateTimeStampedValue<bool?>(false, lastChanged),
            IsCharging = CreateTimeStampedValue(false, now), // Default
        };
        var ocppStates = new ConcurrentDictionary<int, DtoOcppConnectorState>();
        ocppStates.TryAdd(connectorId, ocppState);

        Mock.Mock<ISettings>().Setup(s => s.OcppConnectorStates).Returns(ocppStates);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.TimespanUntilSwitchOn()).Returns(TimeSpan.FromMinutes(1));
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.TimespanUntilSwitchOff()).Returns(TimeSpan.FromMinutes(1));

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();

        var result = await service.GetConstraintValues(null, connectorId, useCar, now, 32m, CancellationToken.None);

        if (useCar)
        {
            Assert.False(result.PhaseReductionAllowed);
        }
        else
        {
            Assert.True(result.PhaseReductionAllowed);
        }
    }

    /// <summary>
    /// Verifies that Phase Increase is allowed when the connector supports Three Phases
    /// and does not support One Phase (or prefers Three).
    /// </summary>
    [Fact]
    public async Task GetConstraintValues_PhaseIncreaseAllowed_Checks()
    {
        var connectorId = 15;
        var connectorEntity = new OcppChargingStationConnector("Test Connector")
        {
            Id = connectorId,
            ConnectedPhasesCount = 3,
            AutoSwitchBetween1And3PhasesEnabled = true,
            ChargeMode = ChargeModeV2.Auto,
        };
        Context.OcppChargingStationConnectors.Add(connectorEntity);
        await Context.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        var lastChanged = now.AddMinutes(-5);

        // For Increase: OnePhase=false, ThreePhase=true
        var ocppState = new DtoOcppConnectorState
        {
            CanHandlePowerOnOnePhase = CreateTimeStampedValue<bool?>(false, lastChanged),
            CanHandlePowerOnThreePhase = CreateTimeStampedValue<bool?>(true, lastChanged),
            IsCharging = CreateTimeStampedValue(true, now),
        };

        var ocppStates = new ConcurrentDictionary<int, DtoOcppConnectorState>();
        ocppStates.TryAdd(connectorId, ocppState);

        Mock.Mock<ISettings>().Setup(s => s.OcppConnectorStates).Returns(ocppStates);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.TimespanUntilSwitchOn()).Returns(TimeSpan.FromMinutes(1));
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.TimespanUntilSwitchOff()).Returns(TimeSpan.FromMinutes(1));

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();

        var result = await service.GetConstraintValues(null, connectorId, false, now, 32m, CancellationToken.None);

        Assert.True(result.PhaseIncreaseAllowed);
        Assert.False(result.PhaseReductionAllowed);
    }

    /// <summary>
    /// Verifies that the resulting MaxCurrent is capped by the global MaxCombinedCurrent setting.
    /// </summary>
    [Fact]
    public async Task GetConstraintValues_MaxCurrentCappedByMaxCombinedCurrent()
    {
         var connectorId = 12;
        var connectorEntity = new OcppChargingStationConnector("Test Connector")
        {
            Id = connectorId,
            MaxCurrent = 32,
        };
        Context.OcppChargingStationConnectors.Add(connectorEntity);
        await Context.SaveChangesAsync();

        Mock.Mock<ISettings>().Setup(s => s.OcppConnectorStates).Returns(new ConcurrentDictionary<int, DtoOcppConnectorState>());
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.TimespanUntilSwitchOn()).Returns(TimeSpan.FromMinutes(1));
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.TimespanUntilSwitchOff()).Returns(TimeSpan.FromMinutes(1));

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();

        var maxCombined = 16m;
        var result = await service.GetConstraintValues(null, connectorId, false, DateTimeOffset.UtcNow, maxCombined, CancellationToken.None);

        Assert.Equal(16, result.MaxCurrent);
    }
}
