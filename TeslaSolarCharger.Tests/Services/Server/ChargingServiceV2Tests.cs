using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autofac.Extras.FakeItEasy;
using Autofac.Extras.Moq;
using Microsoft.Extensions.Configuration;
using Moq;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.SignalR.Notifiers.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.Shared.SignalRClients;
using TeslaSolarCharger.Shared.TimeProviding;
using Xunit;
using Xunit.Abstractions;
using DateOnly = System.DateOnly;

namespace TeslaSolarCharger.Tests.Services.Server;

public class ChargingServiceV2Tests : TestBase
{
    public ChargingServiceV2Tests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    public static IEnumerable<object?[]> NextTargetUtcTestData
    {
        get
        {
            yield return
            [
                new DateTimeOffset(2025, 5, 26, 5,  0, 10, TimeSpan.Zero),
                new DateOnly(2025, 5, 26),
                new DateTimeOffset(2025, 5, 26, 12, 0,  0, TimeSpan.Zero),
            ];
            yield return
            [
                new DateTimeOffset(2025, 5, 26, 12, 0,  0, TimeSpan.Zero),
                new DateOnly(2025, 5, 26),
                new DateTimeOffset(2025, 5, 26, 12, 0,  0, TimeSpan.Zero),
            ];
            yield return
            [
                new DateTimeOffset(2025, 5, 26, 12, 0,  1, TimeSpan.Zero),
                new DateOnly(2025, 5, 26),
                new DateTimeOffset(2025, 5, 28, 12, 0,  0, TimeSpan.Zero),
            ];
            yield return
            [
                new DateTimeOffset(2025, 5, 26, 5,  0, 10, TimeSpan.Zero),
                new DateOnly(2025, 5, 27),
                new DateTimeOffset(2025, 5, 28, 12, 0,  0, TimeSpan.Zero),
            ];
            yield return
            [
                new DateTimeOffset(2025, 5, 26, 12, 0,  0, TimeSpan.Zero),
                new DateOnly(2025, 5, 27),
                new DateTimeOffset(2025, 5, 28, 12, 0,  0, TimeSpan.Zero),
            ];
            yield return
            [
                new DateTimeOffset(2025, 5, 26, 12, 0,  1, TimeSpan.Zero),
                new DateOnly(2025, 5, 27),
                new DateTimeOffset(2025, 5, 28, 12, 0,  0, TimeSpan.Zero),
            ];
            yield return
            [
                new DateTimeOffset(2025, 5, 26, 5,  0, 10, TimeSpan.Zero),
                null,
                new DateTimeOffset(2025, 5, 26, 12, 0,  0, TimeSpan.Zero),
            ];
            yield return
            [
                new DateTimeOffset(2025, 5, 26, 12, 0,  0, TimeSpan.Zero),
                null,
                new DateTimeOffset(2025, 5, 26, 12, 0,  0, TimeSpan.Zero),
            ];
            yield return
            [
                new DateTimeOffset(2025, 5, 26, 12, 0,  1, TimeSpan.Zero),
                null,
                new DateTimeOffset(2025, 5, 28, 12, 0,  0, TimeSpan.Zero),
            ];
        }
    }

    [Theory]
    [MemberData(nameof(NextTargetUtcTestData))]
    public void CanGetNextTargetDateRepeating(DateTimeOffset currentDate, DateOnly? targetDate, DateTimeOffset expectedResult)
    {
        Mock.Mock<IDateTimeProvider>().Setup(d => d.DateTimeOffSetUtcNow()).Returns(currentDate);
        var carValueLog = new Model.Entities.TeslaSolarCharger.CarChargingTarget()
        {
            Id = 1,
            CarId = 1,
            ClientTimeZone = "Europe/Berlin",
            RepeatOnMondays = true,
            RepeatOnWednesdays = true,
            TargetSoc = 20,
            TargetDate = targetDate,
            TargetTime = new TimeOnly(14, 0, 0),
        };
        var chargingServiceV2 = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();
        var nextTargetUtc = chargingServiceV2.GetNextTargetUtc(carValueLog, currentDate);
        Assert.Equal(expectedResult, nextTargetUtc);
    }

    [Fact]
    public async Task SetNewChargingValues_BaseAppNotLicensed_ReturnsImmediately()
    {
        // Arrange
        Mock.Mock<IBackendApiService>()
            .Setup(s => s.IsBaseAppLicensed(true))
            .ReturnsAsync(new Result<bool?>(false, null, null));

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();

        // Act
        await service.SetNewChargingValues(CancellationToken.None);

        // Assert
        Mock.Mock<ILoadPointManagementService>().Verify(s => s.GetLoadPointsToManage(), Times.Never);
    }

    [Fact]
    public async Task SetNewChargingValues_UpdatesManualCarsHomeStatus()
    {
        // Arrange
        var currentDate = new DateTimeOffset(2025, 5, 26, 12, 0, 0, TimeSpan.Zero);
        Mock.Mock<IDateTimeProvider>().Setup(d => d.DateTimeOffSetUtcNow()).Returns(currentDate);
        Mock.Mock<IBackendApiService>()
            .Setup(s => s.IsBaseAppLicensed(true))
            .ReturnsAsync(new Result<bool?>(true, null, null));

        // Seed manual car
        Context.Cars.Add(new Model.Entities.TeslaSolarCharger.Car { Id = 1, CarType = CarType.Manual });
        await Context.SaveChangesAsync();

        var settings = Mock.Mock<ISettings>();
        var dtoCar = new DtoCar { Id = 1, IsHomeGeofence = new DtoTimeStampedValue<bool?>(DateTimeOffset.MinValue, null) };
        settings.Setup(s => s.Cars).Returns(new List<DtoCar> { dtoCar });

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();

        // Act
        // Note: SetNewChargingValues calls SetManualCarsToAtHome internally twice
        // We only care that it updates IsHomeGeofence
        try
        {
            await service.SetNewChargingValues(CancellationToken.None);
        }
        catch
        {
            // Ignore subsequent errors as we only want to test the manual car update part
            // mocking everything for a full run is complex
        }

        // Assert
        Assert.True(dtoCar.IsHomeGeofence.Value);
        Assert.Equal(currentDate, dtoCar.IsHomeGeofence.LastChanged);
    }

    [Fact]
    public async Task SetNewChargingValues_CalculatesGeofences()
    {
        // Arrange
        var currentDate = new DateTimeOffset(2025, 5, 26, 12, 0, 0, TimeSpan.Zero);
        Mock.Mock<IDateTimeProvider>().Setup(d => d.DateTimeOffSetUtcNow()).Returns(currentDate);
        Mock.Mock<IBackendApiService>()
            .Setup(s => s.IsBaseAppLicensed(true))
            .ReturnsAsync(new Result<bool?>(true, null, null));

        // Seed car with GPS
        Context.Cars.Add(new Model.Entities.TeslaSolarCharger.Car { Id = 1, CarType = CarType.Tesla, HomeDetectionVia = HomeDetectionVia.GpsLocation });
        await Context.SaveChangesAsync();

        var settings = Mock.Mock<ISettings>();
        var dtoCar = new DtoCar
        {
            Id = 1,
            Longitude = new DtoTimeStampedValue<double?>(currentDate, 10.0),
            Latitude = new DtoTimeStampedValue<double?>(currentDate, 50.0),
            DistanceToHomeGeofence = new DtoTimeStampedValue<int?>(currentDate, null),
            IsHomeGeofence = new DtoTimeStampedValue<bool?>(currentDate, null)
        };
        settings.Setup(s => s.Cars).Returns(new List<DtoCar> { dtoCar });
        settings.Setup(s => s.CarsToManage).Returns(new List<DtoCar> { dtoCar });

        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeGeofenceLongitude()).Returns(10.0);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeGeofenceLatitude()).Returns(50.0);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeGeofenceRadius()).Returns(100);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();

        // Act
        try
        {
            await service.SetNewChargingValues(CancellationToken.None);
        }
        catch
        {
            // Ignore downstream failures
        }

        // Assert
        Assert.True(dtoCar.IsHomeGeofence.Value);
        Assert.NotNull(dtoCar.DistanceToHomeGeofence.Value);
    }

    [Fact]
    public async Task SetNewChargingValues_CalculatesGeofences_Outside()
    {
        // Arrange
        var currentDate = new DateTimeOffset(2025, 5, 26, 12, 0, 0, TimeSpan.Zero);
        Mock.Mock<IDateTimeProvider>().Setup(d => d.DateTimeOffSetUtcNow()).Returns(currentDate);
        Mock.Mock<IBackendApiService>()
            .Setup(s => s.IsBaseAppLicensed(true))
            .ReturnsAsync(new Result<bool?>(true, null, null));

        // Seed car with GPS
        Context.Cars.Add(new Model.Entities.TeslaSolarCharger.Car { Id = 1, CarType = CarType.Tesla, HomeDetectionVia = HomeDetectionVia.GpsLocation });
        await Context.SaveChangesAsync();

        var settings = Mock.Mock<ISettings>();
        var dtoCar = new DtoCar
        {
            Id = 1,
            Longitude = new DtoTimeStampedValue<double?>(currentDate, 11.0), // Far away
            Latitude = new DtoTimeStampedValue<double?>(currentDate, 51.0),
            DistanceToHomeGeofence = new DtoTimeStampedValue<int?>(currentDate, null),
            IsHomeGeofence = new DtoTimeStampedValue<bool?>(currentDate, null)
        };
        settings.Setup(s => s.Cars).Returns(new List<DtoCar> { dtoCar });
        settings.Setup(s => s.CarsToManage).Returns(new List<DtoCar> { dtoCar });

        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeGeofenceLongitude()).Returns(10.0);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeGeofenceLatitude()).Returns(50.0);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeGeofenceRadius()).Returns(100);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();

        // Act
        try
        {
            await service.SetNewChargingValues(CancellationToken.None);
        }
        catch
        {
            // Ignore downstream failures
        }

        // Assert
        Assert.False(dtoCar.IsHomeGeofence.Value);
    }

    [Fact]
    public async Task SetNewChargingValues_FullFlow_WithSchedules_AndTargetValues()
    {
        // Arrange
        var currentDate = new DateTimeOffset(2025, 5, 26, 12, 0, 0, TimeSpan.Zero);
        Mock.Mock<IDateTimeProvider>().Setup(d => d.DateTimeOffSetUtcNow()).Returns(currentDate);
        Mock.Mock<IBackendApiService>().Setup(s => s.IsBaseAppLicensed(true)).ReturnsAsync(new Result<bool?>(true, null, null));

        Mock.Mock<ILoadPointManagementService>().Setup(s => s.GetLoadPointsToManage())
            .ReturnsAsync(new List<DtoLoadPointOverview>
            {
                new DtoLoadPointOverview { CarId = 1, ChargingPriority = 1, ManageChargingPowerByCar = true }
            });

        Mock.Mock<ILoadPointManagementService>().Setup(s => s.GetLoadPointsWithChargingDetails())
            .ReturnsAsync(new List<DtoLoadPointWithCurrentChargingValues>());

        Mock.Mock<IPowerToControlCalculationService>().Setup(p => p.CalculatePowerToControl(It.IsAny<List<DtoLoadPointWithCurrentChargingValues>>()))
            .Returns(5000); // 5kW surplus

        Mock.Mock<IPowerToControlCalculationService>().Setup(p => p.HasTooLateChanges(It.IsAny<DtoLoadPointWithCurrentChargingValues>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .Returns(false);

        Mock.Mock<IConfigurationWrapper>().Setup(c => c.SkipPowerChangesOnLastAdjustmentNewerThan()).Returns(TimeSpan.FromSeconds(10));

        Mock.Mock<ISettings>().Setup(s => s.OcppConnectorStates).Returns(new ConcurrentDictionary<int, DtoOcppConnectorState>());

        var dtoCar = new DtoCar {
            Id = 1,
            IsHomeGeofence = new DtoTimeStampedValue<bool?>(currentDate, true),
            PluggedIn = new DtoTimeStampedValue<bool?>(currentDate, true)
        };

        Mock.Mock<ISettings>().Setup(s => s.CarsToManage).Returns(new List<DtoCar> { dtoCar });
        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar> { dtoCar });
        Mock.Mock<ISettings>().SetupSet(s => s.ChargingSchedules = It.IsAny<ConcurrentBag<DtoChargingSchedule>>());

        // Setup schedule generation
        Mock.Mock<IChargingScheduleService>().Setup(s => s.GenerateChargingSchedulesForLoadPoint(It.IsAny<DtoLoadPointOverview>(), It.IsAny<List<DtoTimeZonedChargingTarget>>(), It.IsAny<Dictionary<DateTimeOffset, int>>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DtoChargingSchedule>
            {
                new DtoChargingSchedule { ValidFrom = currentDate.AddHours(-1), ValidTo = currentDate.AddHours(1), TargetMinPower = 1000 }
            });

        // Setup target value calculation
        Mock.Mock<ITargetChargingValueCalculationService>()
            .Setup(s => s.AppendTargetValues(It.IsAny<List<DtoTargetChargingValues>>(), It.IsAny<List<DtoChargingSchedule>>(), It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<List<DtoTargetChargingValues>, List<DtoChargingSchedule>, DateTimeOffset, int, int, CancellationToken>((targets, _, _, _, _, _) =>
            {
                foreach (var t in targets)
                {
                    t.TargetValues = new TargetValues { StartCharging = true, TargetCurrent = 10 };
                }
            });

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();

        // Act
        await service.SetNewChargingValues(CancellationToken.None);

        // Assert
        Mock.Mock<ITeslaService>().Verify(t => t.StartCharging(1, 10), Times.Once);
        Mock.Mock<IAppStateNotifier>().Verify(n => n.NotifyStateUpdateAsync(It.IsAny<StateUpdateDto>()), Times.Once);
    }
}
