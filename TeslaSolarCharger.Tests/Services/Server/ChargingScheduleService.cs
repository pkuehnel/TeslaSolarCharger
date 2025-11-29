using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Contracts; // For IConfigurationWrapper
using TeslaSolarCharger.Shared.Dtos.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

public class ChargingScheduleService : TestBase
{
    public ChargingScheduleService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public async Task GenerateChargingSchedulesForLoadPoint_BasicScenario()
    {
        // Arrange
        var currentDate = new DateTimeOffset(2025, 5, 26, 12, 0, 0, TimeSpan.Zero);

        var loadPoint = new DtoLoadPointOverview
        {
            CarId = 1,
            ChargingConnectorId = 1,
            ChargingPriority = 1,
            ManageChargingPowerByCar = true,
        };

        var target = new DtoTimeZonedChargingTarget
        {
            Id = 1,
            TargetSoc = 80,
            CarId = 1,
            NextExecutionTime = currentDate.AddHours(2),
        };

        var chargingTargets = new List<DtoTimeZonedChargingTarget> { target };
        var predictedSurplusSlices = new Dictionary<DateTimeOffset, int>();

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        // Mock car and connector data retrieval
        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar>
        {
            new DtoCar
            {
                Id = 1,
                MaximumAmpere = 16,
                ChargerPhases = new DtoTimeStampedValue<int?>(currentDate, 3),
                SoC = new DtoTimeStampedValue<int?>(currentDate, 50),
                UsableEnergy = 75, // kWh
            },
        });

        // Use in-memory db from TestBase
        Context.OcppChargingStationConnectors.Add(new("test")
        {
            Id = 1,
            MaxCurrent = 16,
            ConnectedPhasesCount = 3,
            AutoSwitchBetween1And3PhasesEnabled = true,
        });
        Context.Cars.Add(new()
        {
            Id = 1,
            UsableEnergy = 75,
            MaximumAmpere = 16,
            MaximumPhases = 3,
            CarType = Shared.Enums.CarType.Tesla,
        });
        await Context.SaveChangesAsync();

        Mock.Mock<IConfigurationWrapper>().Setup(c => c.CarChargeLoss()).Returns(0);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryUsableEnergy()).Returns(10000);

        // Act
        var result = await service.GenerateChargingSchedulesForLoadPoint(loadPoint, chargingTargets, predictedSurplusSlices, currentDate, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains(result, s => s.TargetMinPower > 0);
    }

    [Fact]
    public async Task GenerateChargingSchedulesForLoadPoint_WithSolarSurplus()
    {
        // Arrange
        var currentDate = new DateTimeOffset(2025, 5, 26, 12, 0, 0, TimeSpan.Zero);
        var loadPoint = new DtoLoadPointOverview { CarId = 1, ChargingConnectorId = 1, ManageChargingPowerByCar = true };
        var target = new DtoTimeZonedChargingTarget { Id = 1, TargetSoc = 80, CarId = 1, NextExecutionTime = currentDate.AddHours(5) };
        var chargingTargets = new List<DtoTimeZonedChargingTarget> { target };

        // Predict high surplus for the next few hours
        var predictedSurplusSlices = new Dictionary<DateTimeOffset, int>
        {
            { currentDate.AddHours(0), 5000 },
            { currentDate.AddHours(1), 5000 },
            { currentDate.AddHours(2), 5000 },
        };

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar>
        {
            new DtoCar
            {
                Id = 1,
                MaximumAmpere = 16,
                ChargerPhases = new DtoTimeStampedValue<int?>(currentDate, 3),
                SoC = new DtoTimeStampedValue<int?>(currentDate, 50),
                UsableEnergy = 75,
            },
        });

        Context.OcppChargingStationConnectors.Add(new ("test") { Id = 1, MaxCurrent = 16, ConnectedPhasesCount = 3 });
        Context.Cars.Add(new() { Id = 1, UsableEnergy = 75, MaximumAmpere = 16, MaximumPhases = 3 });
        await Context.SaveChangesAsync();

        Mock.Mock<IConfigurationWrapper>().Setup(c => c.CarChargeLoss()).Returns(0);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryUsableEnergy()).Returns(10000);

        // Act
        var result = await service.GenerateChargingSchedulesForLoadPoint(loadPoint, chargingTargets, predictedSurplusSlices, currentDate, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        // Should align schedules with surplus
        Assert.Contains(result, s => s.EstimatedSolarPower > 0);
    }
}
