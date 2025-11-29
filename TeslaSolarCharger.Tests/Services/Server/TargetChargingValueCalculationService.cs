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

public class TargetChargingValueCalculationService : TestBase
{
    public TargetChargingValueCalculationService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public async Task AppendTargetValues_BasicScenario_SetsTargetValues()
    {
        // Arrange
        var currentDate = new DateTimeOffset(2025, 5, 26, 12, 0, 0, TimeSpan.Zero);
        Mock.Mock<IDateTimeProvider>().Setup(d => d.DateTimeOffSetUtcNow()).Returns(currentDate);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();

        var loadPoint = new DtoLoadPointOverview
        {
            CarId = 1,
            ChargingConnectorId = 1,
            ChargingPriority = 1,
            ManageChargingPowerByCar = true,
        };
        var targetChargingValues = new List<DtoTargetChargingValues>
        {
            new DtoTargetChargingValues(loadPoint),
        };

        var schedules = new List<Shared.Dtos.DtoChargingSchedule>
        {
            new Shared.Dtos.DtoChargingSchedule
            {
                ValidFrom = currentDate.AddHours(-1),
                ValidTo = currentDate.AddHours(1),
                TargetMinPower = 2300, // 10A * 230V
                CarId = 1,
                OcppChargingConnectorId = 1,
            },
        };

        Mock.Mock<TeslaSolarCharger.Server.Services.Contracts.IShouldStartStopChargingCalculator>();
        Mock.Mock<ISettings>().Setup(s => s.OcppConnectorStates).Returns(new System.Collections.Concurrent.ConcurrentDictionary<int, DtoOcppConnectorState>());

        var car = new DtoCar {
                Id = 1,
                MaximumAmpere = 16,
                IsCharging = new DtoTimeStampedValue<bool?>(currentDate, true),
                ChargerPhases = new DtoTimeStampedValue<int?>(currentDate, 1),
            };

        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar> { car });

        // Act
        await service.AppendTargetValues(targetChargingValues, schedules, currentDate, 2300, 0, CancellationToken.None);

        // Assert
        Assert.Single(targetChargingValues);
        Assert.NotNull(targetChargingValues[0].TargetValues);
        Assert.Equal(10, targetChargingValues[0].TargetValues?.TargetCurrent);
    }

    [Theory]
    [InlineData(15, 10, 5)]
    [InlineData(18, 10, 8)]
    public async Task AppendTargetValues_MaxCombinedCurrent_LimitsPower(int maxCombinedCurrent, decimal expectedLp1, decimal expectedLp2)
    {
        // Arrange
        var currentDate = new DateTimeOffset(2025, 5, 26, 12, 0, 0, TimeSpan.Zero);
        Mock.Mock<IDateTimeProvider>().Setup(d => d.DateTimeOffSetUtcNow()).Returns(currentDate);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();

        // Load point 1 (Priority 1)
        var loadPoint1 = new DtoLoadPointOverview { CarId = 1, ChargingConnectorId = 1, ChargingPriority = 1, ManageChargingPowerByCar = true };
        // Load point 2 (Priority 2)
        var loadPoint2 = new DtoLoadPointOverview { CarId = 2, ChargingConnectorId = 2, ChargingPriority = 2, ManageChargingPowerByCar = true };

        var targetChargingValues = new List<DtoTargetChargingValues>
        {
            new DtoTargetChargingValues(loadPoint1),
            new DtoTargetChargingValues(loadPoint2),
        };

        var schedules = new List<Shared.Dtos.DtoChargingSchedule>
        {
            new Shared.Dtos.DtoChargingSchedule { ValidFrom = currentDate.AddHours(-1), ValidTo = currentDate.AddHours(1), TargetMinPower = 2300, CarId = 1, OcppChargingConnectorId = 1 },
            new Shared.Dtos.DtoChargingSchedule { ValidFrom = currentDate.AddHours(-1), ValidTo = currentDate.AddHours(1), TargetMinPower = 2300, CarId = 2, OcppChargingConnectorId = 2 },
        };

        Mock.Mock<TeslaSolarCharger.Server.Services.Contracts.IShouldStartStopChargingCalculator>();
        Mock.Mock<ISettings>().Setup(s => s.OcppConnectorStates).Returns(new System.Collections.Concurrent.ConcurrentDictionary<int, DtoOcppConnectorState>());

        var car1 = new DtoCar {
            Id = 1,
            MaximumAmpere = 16,
            IsCharging = new DtoTimeStampedValue<bool?>(currentDate, true),
            ChargerPhases = new DtoTimeStampedValue<int?>(currentDate, 1),
        };

        var car2 = new DtoCar {
            Id = 2,
            MaximumAmpere = 16,
            IsCharging = new DtoTimeStampedValue<bool?>(currentDate, true),
            ChargerPhases = new DtoTimeStampedValue<int?>(currentDate, 1),
        };

        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar> { car1, car2 });

        Mock.Mock<IConfigurationWrapper>().Setup(c => c.MaxCombinedCurrent()).Returns(maxCombinedCurrent);

        // Act
        await service.AppendTargetValues(targetChargingValues, schedules, currentDate, 4600, 0, CancellationToken.None);

        // Assert
        Assert.Equal(2, targetChargingValues.Count);

        // Priority 1
        Assert.NotNull(targetChargingValues[0].TargetValues);
        Assert.Equal(expectedLp1, targetChargingValues[0].TargetValues?.TargetCurrent);

        // Priority 2
        Assert.NotNull(targetChargingValues[1].TargetValues);
        Assert.Equal(expectedLp2, targetChargingValues[1].TargetValues?.TargetCurrent);
    }
}
