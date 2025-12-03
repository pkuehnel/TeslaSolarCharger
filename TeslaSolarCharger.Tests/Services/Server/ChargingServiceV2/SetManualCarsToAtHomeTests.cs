using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac.Extras.Moq;
using Moq;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingServiceV2;

public class SetManualCarsToAtHomeTests : TestBase
{
    public SetManualCarsToAtHomeTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    public static IEnumerable<object[]> GetScenarios()
    {
        // 1. No cars in DB. Nothing happens.
        yield return new object[]
        {
            new List<Car>(),
            new List<DtoCar>(),
            new List<int>() // Expected updated Car IDs
        };

        // 2. Manual car in DB, but not in Settings. Nothing updated.
        yield return new object[]
        {
            new List<Car> { new() { Id = 1, CarType = CarType.Manual } },
            new List<DtoCar>(),
            new List<int>()
        };

        // 3. Manual car in DB and Settings. Should update.
        yield return new object[]
        {
            new List<Car> { new() { Id = 1, CarType = CarType.Manual } },
            new List<DtoCar> { new() { Id = 1, IsHomeGeofence = new(DateTimeOffset.MinValue, false) } },
            new List<int> { 1 }
        };

        // 4. Tesla car in DB and Settings. Should NOT update.
        yield return new object[]
        {
            new List<Car> { new() { Id = 2, CarType = CarType.Tesla } },
            new List<DtoCar> { new() { Id = 2, IsHomeGeofence = new(DateTimeOffset.MinValue, false) } },
            new List<int>()
        };

        // 5. Multiple cars: 1 Manual (match), 1 Manual (no match), 1 Tesla.
        yield return new object[]
        {
            new List<Car>
            {
                new() { Id = 1, CarType = CarType.Manual }, // Should update
                new() { Id = 2, CarType = CarType.Manual }, // No settings match
                new() { Id = 3, CarType = CarType.Tesla }   // Should not update
            },
            new List<DtoCar>
            {
                new() { Id = 1, IsHomeGeofence = new(DateTimeOffset.MinValue, false) },
                new() { Id = 3, IsHomeGeofence = new(DateTimeOffset.MinValue, false) }
            },
            new List<int> { 1 }
        };

        // 6. Manual car already at home. Value is same, so NO update to LastChanged.
        yield return new object[]
        {
             new List<Car> { new() { Id = 1, CarType = CarType.Manual } },
             new List<DtoCar> { new() { Id = 1, IsHomeGeofence = new(DateTimeOffset.MinValue.AddDays(1), true) } }, // Already true
             new List<int>() // Expect NO change
        };
    }

    [Theory]
    [MemberData(nameof(GetScenarios))]
    public async Task SetManualCarsToAtHome_Scenarios(
        List<Car> dbCars,
        List<DtoCar> settingsCars,
        List<int> expectedUpdatedCarIds)
    {
        // Arrange
        // Seed DB
        Context.Cars.AddRange(dbCars);
        await Context.SaveChangesAsync();

        // Setup Settings Mock
        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.Setup(s => s.Cars).Returns(settingsCars);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();
        var currentDate = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

        // Pre-record state of settingsCars to detect changes
        var initialStates = settingsCars.ToDictionary(c => c.Id, c => c.IsHomeGeofence.LastChanged);

        // Act
        service.SetManualCarsToAtHome(currentDate);

        // Assert
        foreach (var car in settingsCars)
        {
            bool shouldHaveUpdated = expectedUpdatedCarIds.Contains(car.Id);

            if (shouldHaveUpdated)
            {
                Assert.True(car.IsHomeGeofence.Value, $"Car {car.Id} should be at home");
                Assert.Equal(currentDate, car.IsHomeGeofence.LastChanged); // Should match update time
            }
            else
            {
                // Should be unchanged
                Assert.Equal(initialStates[car.Id], car.IsHomeGeofence.LastChanged);
            }
        }
    }
}
