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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GenerateChargingSchedulesForLoadPoint_BasicScenario(bool manageChargingPowerByCar)
    {
        // Arrange
        var currentDate = new DateTimeOffset(2025, 5, 26, 12, 0, 0, TimeSpan.Zero);

        var loadPoint = new DtoLoadPointOverview
        {
            CarId = 1,
            ChargingConnectorId = 1,
            ChargingPriority = 1,
            ManageChargingPowerByCar = manageChargingPowerByCar,
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

        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar>
        {
            new DtoCar
            {
                Id = 1,
                Vin = "vin",
                MaximumAmpere = 16,
                MinimumAmpere = 6,
                ChargeModeV2 = Shared.Enums.ChargeModeV2.Auto,
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
            MinCurrent = 6,
            ConnectedPhasesCount = 3,
            AutoSwitchBetween1And3PhasesEnabled = true,
        });
        Context.Cars.Add(new()
        {
            Id = 1,
            UsableEnergy = 75,
            MaximumAmpere = 16,
            MinimumAmpere = 6,
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

    [Theory]
    [InlineData(5000, 6000)]
    [InlineData(7000, 5500)]
    public async Task GenerateChargingSchedulesForLoadPoint_WithSolarSurplus(int firstSlice, int secondSlice)
    {
        // Arrange
        var currentDate = new DateTimeOffset(2025, 5, 26, 12, 0, 0, TimeSpan.Zero);
        var loadPoint = new DtoLoadPointOverview { CarId = 1, ChargingConnectorId = 1, ManageChargingPowerByCar = true };
        var target = new DtoTimeZonedChargingTarget { Id = 1, TargetSoc = 80, CarId = 1, NextExecutionTime = currentDate.AddHours(5) };
        var chargingTargets = new List<DtoTimeZonedChargingTarget> { target };

        // Predict high surplus for the next few hours
        var predictedSurplusSlices = new Dictionary<DateTimeOffset, int>
        {
            { currentDate.AddHours(0), firstSlice },
            { currentDate.AddHours(1), secondSlice },
            { currentDate.AddHours(2), firstSlice },
        };

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar>
        {
            new DtoCar
            {
                Id = 1,
                Vin = "vin",
                MaximumAmpere = 16,
                MinimumAmpere = 6,
                ChargeModeV2 = Shared.Enums.ChargeModeV2.Auto,
                ChargerPhases = new DtoTimeStampedValue<int?>(currentDate, 3),
                SoC = new DtoTimeStampedValue<int?>(currentDate, 50),
                UsableEnergy = 75,
            },
        });

        Context.OcppChargingStationConnectors.Add(new ("test")
        {
            Id = 1,
            MaxCurrent = 16,
            MinCurrent = 6,
            ConnectedPhasesCount = 3,
            AutoSwitchBetween1And3PhasesEnabled = true,
        });
        Context.Cars.Add(new()
        {
            Id = 1,
            UsableEnergy = 75,
            MaximumAmpere = 16,
            MinimumAmpere = 6,
            MaximumPhases = 3,
            CarType = Shared.Enums.CarType.Tesla,
        });
        await Context.SaveChangesAsync();

        Mock.Mock<IConfigurationWrapper>().Setup(c => c.CarChargeLoss()).Returns(0);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryUsableEnergy()).Returns(10000);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.UsePredictedSolarPowerGenerationForChargingSchedules()).Returns(true);

        // Act
        var result = await service.GenerateChargingSchedulesForLoadPoint(loadPoint, chargingTargets, predictedSurplusSlices, currentDate, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        // Should align schedules with surplus
        Assert.Contains(result, s => s.EstimatedSolarPower >= Math.Min(firstSlice, secondSlice));
    }

    [Fact]
    public async Task GenerateChargingSchedulesForLoadPoint_ReturnsEmpty_WhenCarNotAuto()
    {
        // Arrange
        var currentDate = new DateTimeOffset(2025, 5, 26, 12, 0, 0, TimeSpan.Zero);
        var loadPoint = new DtoLoadPointOverview { CarId = 1, ChargingConnectorId = 1, ManageChargingPowerByCar = true };
        var target = new DtoTimeZonedChargingTarget { Id = 1, TargetSoc = 80, CarId = 1, NextExecutionTime = currentDate.AddHours(5) };
        var chargingTargets = new List<DtoTimeZonedChargingTarget> { target };
        var predictedSurplusSlices = new Dictionary<DateTimeOffset, int>();

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar>
        {
            new DtoCar
            {
                Id = 1,
                Vin = "vin",
                MaximumAmpere = 16,
                MinimumAmpere = 6,
                ChargeModeV2 = Shared.Enums.ChargeModeV2.Manual,
                ChargerPhases = new DtoTimeStampedValue<int?>(currentDate, 3),
                SoC = new DtoTimeStampedValue<int?>(currentDate, 50),
                UsableEnergy = 75,
            },
        });

        Context.OcppChargingStationConnectors.Add(new ("test")
        {
            Id = 1,
            MaxCurrent = 16,
            MinCurrent = 6,
            ConnectedPhasesCount = 3,
            AutoSwitchBetween1And3PhasesEnabled = true,
        });
        Context.Cars.Add(new()
        {
            Id = 1,
            UsableEnergy = 75,
            MaximumAmpere = 16,
            MinimumAmpere = 6,
            MaximumPhases = 3,
            CarType = Shared.Enums.CarType.Tesla,
        });
        await Context.SaveChangesAsync();

        Mock.Mock<IConfigurationWrapper>().Setup(c => c.CarChargeLoss()).Returns(0);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryUsableEnergy()).Returns(10000);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.UsePredictedSolarPowerGenerationForChargingSchedules()).Returns(true);

        // Act
        var result = await service.GenerateChargingSchedulesForLoadPoint(loadPoint, chargingTargets, predictedSurplusSlices, currentDate, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
