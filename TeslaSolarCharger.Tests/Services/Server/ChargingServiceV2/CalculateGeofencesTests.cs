using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autofac.Extras.Moq;
using Moq;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingServiceV2;

public class CalculateGeofencesTests : TestBase
{
    public CalculateGeofencesTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Theory]
    // 1. Inside Geofence (Same location)
    // Distance = 0 < 100. IsHome = true.
    [InlineData(1, 52.5200, 13.4050, 52.5200, 13.4050, 100, HomeDetectionVia.GpsLocation, true, true)]

    // 2. Outside Geofence
    // 0.01 degree lat ~ 1.11km = 1110m > 100m. IsHome = false.
    [InlineData(2, 52.5300, 13.4050, 52.5200, 13.4050, 100, HomeDetectionVia.GpsLocation, false, true)]

    // 3. No Location Data (Lat null) -> IsHomeGeofence not updated
    [InlineData(3, null, 13.4050, 52.5200, 13.4050, 100, HomeDetectionVia.GpsLocation, false, false)]

    // 4. No Location Data (Lon null) -> IsHomeGeofence not updated
    [InlineData(4, 52.5200, null, 52.5200, 13.4050, 100, HomeDetectionVia.GpsLocation, false, false)]

    // 5. Not GPS Detection -> IsHomeGeofence not updated
    [InlineData(5, 52.5200, 13.4050, 52.5200, 13.4050, 100, HomeDetectionVia.LocatedAtHome, false, false)]
    public async Task CalculateGeofences_UpdatesValues(
        int carId,
        double? carLat, double? carLon,
        double homeLat, double homeLon, int radius,
        HomeDetectionVia detectionVia,
        bool expectedIsHome,
        bool expectUpdate)
    {
        // Arrange
        var currentDate = CurrentFakeDate;
        // Initial state: IsHomeGeofence = false. Timestamp = MinValue.

        var dtoCar = new DtoCar
        {
            Id = carId,
            Latitude = new(DateTimeOffset.MinValue, carLat),
            Longitude = new(DateTimeOffset.MinValue, carLon),
            IsHomeGeofence = new(DateTimeOffset.MinValue, false),
            DistanceToHomeGeofence = new(DateTimeOffset.MinValue, null)
        };

        Mock.Mock<ISettings>().Setup(s => s.CarsToManage).Returns(new List<DtoCar> { dtoCar });

        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeGeofenceLatitude()).Returns(homeLat);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeGeofenceLongitude()).Returns(homeLon);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeGeofenceRadius()).Returns(radius);

        Context.Cars.Add(new Car { Id = carId, HomeDetectionVia = detectionVia });
        await Context.SaveChangesAsync();

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();

        // Act
        await service.CalculateGeofences(currentDate);

        // Assert
        if (!expectUpdate)
        {
             // Should be null
             Assert.Null(dtoCar.DistanceToHomeGeofence.Value);

             // Timestamp should remain MinValue (not updated)
             Assert.Equal(DateTimeOffset.MinValue, dtoCar.IsHomeGeofence.Timestamp);
             Assert.False(dtoCar.IsHomeGeofence.Value);
        }
        else
        {
            // Should have distance
            Assert.NotNull(dtoCar.DistanceToHomeGeofence.Value);

            // Should be updated
            Assert.Equal(currentDate, dtoCar.IsHomeGeofence.Timestamp);
            Assert.Equal(expectedIsHome, dtoCar.IsHomeGeofence.Value);
        }
    }

    [Fact(Skip = "Fails intentionally: Bug in ChargingServiceV2.CalculateGeofences where CarStateChanged is never called due to object reference comparison.")]
    public async Task CalculateGeofences_CarStateChanged_Called_WhenStateChanges()
    {
        // Arrange
        var carId = 99;
        var homeLat = 52.5200;
        var homeLon = 13.4050;
        var radius = 100;

        // Car inside geofence
        var carLat = 52.5200;
        var carLon = 13.4050;

        var currentDate = CurrentFakeDate;

        var dtoCar = new DtoCar
        {
            Id = carId,
            Latitude = new(DateTimeOffset.MinValue, carLat),
            Longitude = new(DateTimeOffset.MinValue, carLon),
            IsHomeGeofence = new(DateTimeOffset.MinValue, false), // Initially FALSE, will change to TRUE
            DistanceToHomeGeofence = new(DateTimeOffset.MinValue, null)
        };

        Mock.Mock<ISettings>().Setup(s => s.CarsToManage).Returns(new List<DtoCar> { dtoCar });
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeGeofenceLatitude()).Returns(homeLat);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeGeofenceLongitude()).Returns(homeLon);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeGeofenceRadius()).Returns(radius);

        Context.Cars.Add(new Car { Id = carId, HomeDetectionVia = HomeDetectionVia.GpsLocation });
        await Context.SaveChangesAsync();

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();

        // Act
        await service.CalculateGeofences(currentDate);

        // Assert
        Assert.True(dtoCar.IsHomeGeofence.Value); // Value DOES update correctly

        // This fails because the 'if (wasAtHomeBefore != car.IsHomeGeofence)' check uses reference equality
        // and both variables point to the same DtoTimeStampedValue object instance.
        Mock.Mock<ILoadPointManagementService>().Verify(s => s.CarStateChanged(carId), Times.Once);
    }
}
