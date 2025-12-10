using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autofac.Extras.Moq;
using Moq;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Dtos.Settings;
using Xunit;
using Xunit.Abstractions;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingServiceV2;

public class GenerateChargingSchedulesTests : TestBase
{
    private readonly ITestOutputHelper _output;

    public GenerateChargingSchedulesTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
        _output = outputHelper;
    }

    [Theory]
    [MemberData(nameof(GetScenarios))]
    public async Task GenerateChargingSchedules_Scenarios(
        string description,
        DateTimeOffset currentDate,
        List<DtoLoadPointOverview> loadPoints,
        List<CarChargingTarget> targets,
        List<DtoCar> cars,
        Dictionary<DateTimeOffset, int> predictedSurplus,
        List<DtoChargingSchedule> mockServiceResponse,
        int expectedScheduleCount,
        int expectedRelevantTargetsCount)
    {
        _output.WriteLine($"Running Scenario: {description}");

        // Arrange
        // 1. Setup Context with Targets
        if (targets != null)
        {
            Context.CarChargingTargets.AddRange(targets);
            await Context.SaveChangesAsync();
        }

        // 2. Setup Settings with Cars
        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.Setup(s => s.Cars).Returns(cars ?? new List<DtoCar>());

        // 3. Setup EnergyService
        var energyServiceMock = Mock.Mock<IEnergyDataService>();
        energyServiceMock.Setup(s => s.GetPredictedSurplusPerSlice(
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(predictedSurplus ?? new Dictionary<DateTimeOffset, int>());

        // 4. Setup ChargingScheduleService
        var scheduleServiceMock = Mock.Mock<IChargingScheduleService>();
        scheduleServiceMock.Setup(s => s.GenerateChargingSchedulesForLoadPoint(
                It.IsAny<DtoLoadPointOverview>(),
                It.IsAny<List<DtoTimeZonedChargingTarget>>(),
                It.IsAny<Dictionary<DateTimeOffset, int>>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<List<DtoChargingSchedule>>()))
            .ReturnsAsync((DtoLoadPointOverview lp, List<DtoTimeZonedChargingTarget> t, Dictionary<DateTimeOffset, int> p, DateTimeOffset d, CancellationToken c, List<DtoChargingSchedule> s) =>
            {
                // Verify expected relevant targets count if loadpoint has a car
                if (lp.CarId.HasValue)
                {
                     Assert.Equal(expectedRelevantTargetsCount, t.Count);
                }
                else
                {
                     Assert.Empty(t);
                }

                return mockServiceResponse ?? new List<DtoChargingSchedule>();
            });

        var sut = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();

        // Act
        var result = await sut.GenerateChargingSchedules(currentDate, loadPoints, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedScheduleCount, result.Count);
    }

    public static IEnumerable<object[]> GetScenarios()
    {
        var now = new DateTimeOffset(2023, 10, 1, 12, 0, 0, TimeSpan.Zero);

        // Scenario 1: Empty LoadPoints
        yield return new object[]
        {
            "Empty LoadPoints",
            now,
            new List<DtoLoadPointOverview>(),
            new List<CarChargingTarget>(),
            new List<DtoCar>(),
            new Dictionary<DateTimeOffset, int>(),
            new List<DtoChargingSchedule>(),
            0,
            0
        };

        // Scenario 2: LoadPoint without Car
        yield return new object[]
        {
            "LoadPoint without Car",
            now,
            new List<DtoLoadPointOverview>
            {
                new DtoLoadPointOverview { CarId = null, ChargingConnectorId = 1 }
            },
            new List<CarChargingTarget>(),
            new List<DtoCar>(),
            new Dictionary<DateTimeOffset, int>(),
            new List<DtoChargingSchedule> { new DtoChargingSchedule(1, null, 0, 230, 3, new()) }, // Mock returns 1
            1,
            0
        };

        // Scenario 3: LoadPoint with Car, No Targets
        var car1 = new DtoCar { Id = 101, Name = "Tesla" };
        yield return new object[]
        {
            "LoadPoint with Car, No Targets",
            now,
            new List<DtoLoadPointOverview>
            {
                new DtoLoadPointOverview { CarId = 101, ChargingConnectorId = 1 }
            },
            new List<CarChargingTarget>(),
            new List<DtoCar> { car1 },
            new Dictionary<DateTimeOffset, int>(),
            new List<DtoChargingSchedule>(),
            0,
            0
        };

        // Scenario 4: LoadPoint with Car, Target in Future
        var targetFuture = new CarChargingTarget
        {
            Id = 1, CarId = 101,
            TargetDate = DateOnly.FromDateTime(now.AddDays(1).Date),
            TargetTime = new TimeOnly(8, 0)
        };
        // We expect service to be called and return schedules
        // Expected relevant targets: 1
        yield return new object[]
        {
            "LoadPoint with Car, Target Future",
            now,
            new List<DtoLoadPointOverview>
            {
                new DtoLoadPointOverview { CarId = 101, ChargingConnectorId = 1 }
            },
            new List<CarChargingTarget> { targetFuture },
            new List<DtoCar> { car1 },
            new Dictionary<DateTimeOffset, int>(),
            new List<DtoChargingSchedule> { new DtoChargingSchedule(1, null, 0, 230, 3, new()) },
            1,
            1
        };

        // Scenario 5: LoadPoint with Car, Target fulfilled
        var targetFulfilled = new CarChargingTarget
        {
            Id = 2, CarId = 101,
            TargetDate = DateOnly.FromDateTime(now.AddDays(1).Date),
            TargetTime = new TimeOnly(8, 0),
            LastFulFilled = now.AddHours(1)
        };
        // LastFulFilled (now+1h) > now -> Filtered out
        // Expected relevant targets: 0
        yield return new object[]
        {
            "LoadPoint with Car, Target Fulfilled",
            now,
            new List<DtoLoadPointOverview>
            {
                new DtoLoadPointOverview { CarId = 101, ChargingConnectorId = 1 }
            },
            new List<CarChargingTarget> { targetFulfilled },
            new List<DtoCar> { car1 },
            new Dictionary<DateTimeOffset, int>(),
            new List<DtoChargingSchedule> { new DtoChargingSchedule(1, null, 0, 230, 3, new()) },
            1,
            0
        };
    }
}
