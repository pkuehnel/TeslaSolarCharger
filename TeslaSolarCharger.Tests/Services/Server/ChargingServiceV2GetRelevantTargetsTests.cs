using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

public class ChargingServiceV2GetRelevantTargetsTests : TestBase
{
    public ChargingServiceV2GetRelevantTargetsTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Fact]
    public async Task GetRelevantTargets_NoTargetsInDb_ReturnsEmptyList()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();
        var carId = 1;
        var carIds = new[] { carId };
        var currentDate = new DateTimeOffset(2023, 10, 1, 12, 0, 0, TimeSpan.Zero);

        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar>
        {
            new DtoCar { Id = carId }
        });

        // Act
        var result = await service.GetRelevantTargets(carIds, currentDate, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetRelevantTargets_FulfilledTarget_ReturnsEmptyList()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();
        var carId = 1;
        var carIds = new[] { carId };
        var currentDate = new DateTimeOffset(2023, 10, 1, 12, 0, 0, TimeSpan.Zero);

        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar>
        {
            new DtoCar { Id = carId }
        });

        Context.CarChargingTargets.Add(new CarChargingTarget
        {
            CarId = carId,
            TargetDate = DateOnly.FromDateTime(currentDate.Date),
            TargetTime = TimeOnly.FromTimeSpan(currentDate.TimeOfDay.Add(TimeSpan.FromHours(1))),
            LastFulFilled = currentDate.AddMinutes(1)
        });
        await Context.SaveChangesAsync();

        // Act
        var result = await service.GetRelevantTargets(carIds, currentDate, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetRelevantTargets_SingleOneTimeTarget_ReturnsTarget()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();
        var carId = 1;
        var carIds = new[] { carId };
        var currentDate = new DateTimeOffset(2023, 10, 1, 12, 0, 0, TimeSpan.Zero); // Sunday

        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar>
        {
            new DtoCar { Id = carId }
        });

        var targetTime = currentDate.AddHours(2);
        Context.CarChargingTargets.Add(new CarChargingTarget
        {
            CarId = carId,
            TargetDate = DateOnly.FromDateTime(targetTime.Date),
            TargetTime = TimeOnly.FromTimeSpan(targetTime.TimeOfDay),
            ClientTimeZone = "UTC"
        });
        await Context.SaveChangesAsync();

        // Act
        var result = await service.GetRelevantTargets(carIds, currentDate, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal(targetTime, result[0].NextExecutionTime);
    }

    [Fact]
    public async Task GetRelevantTargets_OverdueTarget_ReturnsTarget()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();
        var carId = 1;
        var carIds = new[] { carId };
        var currentDate = new DateTimeOffset(2023, 10, 1, 12, 0, 0, TimeSpan.Zero);

        var pluggedIn = new DtoTimeStampedValue<bool?>(DateTimeOffset.MinValue, false);
        pluggedIn.Update(currentDate.AddHours(-10), true);

        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar>
        {
            new DtoCar
            {
                Id = carId,
                PluggedIn = pluggedIn
            }
        });

        // Target was 1 hour ago
        var targetTime = currentDate.AddHours(-1);
        Context.CarChargingTargets.Add(new CarChargingTarget
        {
            CarId = carId,
            TargetDate = DateOnly.FromDateTime(targetTime.Date),
            TargetTime = TimeOnly.FromTimeSpan(targetTime.TimeOfDay),
            ClientTimeZone = "UTC"
        });
        await Context.SaveChangesAsync();

        // Act
        var result = await service.GetRelevantTargets(carIds, currentDate, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal(targetTime, result[0].NextExecutionTime);
    }

    [Theory]
    [InlineData("Monday", true)]
    [InlineData("Tuesday", true)]
    public async Task GetRelevantTargets_RecurringTarget_ChecksDayOfWeek(string dayOfWeek, bool shouldFindTarget)
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();
        var carId = 1;
        var carIds = new[] { carId };

        var currentDate = new DateTimeOffset(2023, 10, 1, 12, 0, 0, TimeSpan.Zero); // Sunday

        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar>
        {
            new DtoCar { Id = carId }
        });

        // Target for 14:00
        var target = new CarChargingTarget
        {
            CarId = carId,
            TargetTime = new TimeOnly(14, 0),
            ClientTimeZone = "UTC"
        };

        DayOfWeek targetDay = (DayOfWeek)Enum.Parse(typeof(DayOfWeek), dayOfWeek);
        if (targetDay == DayOfWeek.Monday) target.RepeatOnMondays = true;
        if (targetDay == DayOfWeek.Tuesday) target.RepeatOnTuesdays = true;

        Context.CarChargingTargets.Add(target);
        await Context.SaveChangesAsync();

        // Act
        var result = await service.GetRelevantTargets(carIds, currentDate, CancellationToken.None);

        // Assert
        if (shouldFindTarget)
        {
            Assert.Single(result);
            Assert.Equal(targetDay, result[0].NextExecutionTime.DayOfWeek);
            Assert.Equal(14, result[0].NextExecutionTime.Hour);
        }
        else
        {
            Assert.Empty(result);
        }
    }

    [Theory]
    [InlineData(true, 1)] // Repeats on Monday (Tomorrow) -> Found
    [InlineData(false, 0)] // Repeats nowhere -> Not Found
    public async Task GetRelevantTargets_RecurringTarget_Monday_FromSunday(bool repeatOnMonday, int expectedCount)
    {
         // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();
        var carId = 1;
        var carIds = new[] { carId };
        var currentDate = new DateTimeOffset(2023, 10, 1, 12, 0, 0, TimeSpan.Zero); // Sunday

        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar>
        {
            new DtoCar { Id = carId }
        });

        var target = new CarChargingTarget
        {
            CarId = carId,
            TargetTime = new TimeOnly(14, 0),
            ClientTimeZone = "UTC",
            RepeatOnMondays = repeatOnMonday
        };

        Context.CarChargingTargets.Add(target);
        await Context.SaveChangesAsync();

        // Act
        var result = await service.GetRelevantTargets(carIds, currentDate, CancellationToken.None);

        // Assert
        Assert.Equal(expectedCount, result.Count);
        if (expectedCount > 0)
        {
             // Next Monday from Oct 1st is Oct 2nd
             Assert.Equal(2, result[0].NextExecutionTime.Day);
        }
    }

    [Fact]
    public async Task GetRelevantTargets_MultipleTargets_ReturnsOrderedByExecutionTime()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();
        var carId = 1;
        var carIds = new[] { carId };
        var currentDate = new DateTimeOffset(2023, 10, 1, 12, 0, 0, TimeSpan.Zero); // Sunday

        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar>
        {
            new DtoCar { Id = carId }
        });

        // Target 1: Monday (Tomorrow)
        Context.CarChargingTargets.Add(new CarChargingTarget
        {
            CarId = carId,
            TargetTime = new TimeOnly(10, 0),
            RepeatOnMondays = true,
            ClientTimeZone = "UTC"
        });

        // Target 2: Tuesday (Day after tomorrow)
        Context.CarChargingTargets.Add(new CarChargingTarget
        {
            CarId = carId,
            TargetTime = new TimeOnly(10, 0),
            RepeatOnTuesdays = true,
            ClientTimeZone = "UTC"
        });

        // Target 3: Today (Sunday) later
        Context.CarChargingTargets.Add(new CarChargingTarget
        {
            CarId = carId,
            TargetTime = new TimeOnly(14, 0),
            RepeatOnSundays = true,
            ClientTimeZone = "UTC"
        });

        await Context.SaveChangesAsync();

        // Act
        var result = await service.GetRelevantTargets(carIds, currentDate, CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Count);
        // Order should be: Sunday 14:00, Monday 10:00, Tuesday 10:00
        Assert.Equal(DayOfWeek.Sunday, result[0].NextExecutionTime.DayOfWeek);
        Assert.Equal(DayOfWeek.Monday, result[1].NextExecutionTime.DayOfWeek);
        Assert.Equal(DayOfWeek.Tuesday, result[2].NextExecutionTime.DayOfWeek);
    }

    [Fact]
    public async Task GetRelevantTargets_FiltersByCarId()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();
        var carId1 = 1;
        var carId2 = 2;
        var carIds = new[] { carId1 }; // Only asking for car 1
        var currentDate = new DateTimeOffset(2023, 10, 1, 12, 0, 0, TimeSpan.Zero);

        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar>
        {
            new DtoCar { Id = carId1 },
            new DtoCar { Id = carId2 }
        });

        Context.CarChargingTargets.Add(new CarChargingTarget
        {
            CarId = carId1,
            TargetTime = new TimeOnly(14, 0),
            RepeatOnSundays = true,
            ClientTimeZone = "UTC"
        });

        Context.CarChargingTargets.Add(new CarChargingTarget
        {
            CarId = carId2,
            TargetTime = new TimeOnly(14, 0),
            RepeatOnSundays = true,
            ClientTimeZone = "UTC"
        });

        await Context.SaveChangesAsync();

        // Act
        var result = await service.GetRelevantTargets(carIds, currentDate, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal(carId1, result[0].CarId);
    }

    [Fact]
    public async Task GetRelevantTargets_TimeZoneHandling_CorrectlyConverts()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();
        var carId = 1;
        var carIds = new[] { carId };
        var currentDate = new DateTimeOffset(2023, 10, 1, 10, 0, 0, TimeSpan.Zero); // UTC 10:00

        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar>
        {
            new DtoCar { Id = carId }
        });

        // Target: 10:00 in "Tokyo Standard Time" (UTC+9)
        // 10:00 TST is 01:00 UTC of the same day.
        // Current date is 10:00 UTC. 01:00 UTC is in the past.
        // Wait, if today is Oct 1.
        // 10:00 TST is Oct 1 01:00 UTC.
        // Current is Oct 1 10:00 UTC.
        // So target is in the past for today.

        // Let's make target 20:00 TST (UTC+9).
        // 20:00 TST = 11:00 UTC.
        // Current is 10:00 UTC.
        // So target should be returned as 11:00 UTC.

        var targetTime = new TimeOnly(20, 0);
        // Use a timezone ID that is likely available.
        // "Tokyo Standard Time" works on Windows. On Linux it depends on installed ICU data.
        // "Asia/Tokyo" works on Linux/ICU.
        // To be safe cross-platform, "UTC" is best, but we want to test conversion.
        // .NET Core on Linux usually handles IANA IDs. "Tokyo Standard Time" might fail if not mapped.
        // The codebase uses "Europe/Berlin" in existing tests. I'll use "Europe/London" (UTC+1 in summer).
        // Oct 1st is likely British Summer Time (BST, UTC+1).

        var timeZoneId = "Europe/London";
        // Target 12:00 London time.
        // If BST (UTC+1), 12:00 London = 11:00 UTC.
        // Current is 10:00 UTC.
        // Result should be 11:00 UTC.

        Context.CarChargingTargets.Add(new CarChargingTarget
        {
            CarId = carId,
            TargetTime = new TimeOnly(12, 0),
            RepeatOnSundays = true,
            ClientTimeZone = timeZoneId
        });
        await Context.SaveChangesAsync();

        // Act
        var result = await service.GetRelevantTargets(carIds, currentDate, CancellationToken.None);

        // Assert
        Assert.Single(result);
        // Expect 11:00 UTC
        Assert.Equal(11, result[0].NextExecutionTime.Hour);
        Assert.Equal(TimeSpan.Zero, result[0].NextExecutionTime.Offset);
    }

    [Fact]
    public async Task GetRelevantTargets_WithPluggedInTime_UsesPluggedInTimeForEarliestExecution()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();
        var carId = 1;
        var carIds = new[] { carId };
        // Current date: Sunday 12:00
        var currentDate = new DateTimeOffset(2023, 10, 1, 12, 0, 0, TimeSpan.Zero);

        // Car plugged in at 11:00
        var pluggedInTime = currentDate.AddHours(-1); // 11:00

        var pluggedIn = new DtoTimeStampedValue<bool?>(DateTimeOffset.MinValue, false);
        pluggedIn.Update(pluggedInTime, true);

        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar>
        {
            new DtoCar
            {
                Id = carId,
                PluggedIn = pluggedIn
            }
        });

        Context.CarChargingTargets.Add(new CarChargingTarget
        {
            CarId = carId,
            TargetTime = new TimeOnly(11, 30),
            RepeatOnSundays = true,
            ClientTimeZone = "UTC"
        });
        await Context.SaveChangesAsync();

        // Act
        var result = await service.GetRelevantTargets(carIds, currentDate, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal(11, result[0].NextExecutionTime.Hour);
        Assert.Equal(30, result[0].NextExecutionTime.Minute);
    }

    [Fact]
    public async Task GetRelevantTargets_TargetDateInPast_ButRepeating_ReturnsNextOccurrence()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();
        var carId = 1;
        var carIds = new[] { carId };
        var currentDate = new DateTimeOffset(2023, 10, 1, 12, 0, 0, TimeSpan.Zero); // Sunday

        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar>
        {
            new DtoCar { Id = carId }
        });

        var targetDate = new DateOnly(2022, 1, 1);
        Context.CarChargingTargets.Add(new CarChargingTarget
        {
            CarId = carId,
            TargetDate = targetDate,
            TargetTime = new TimeOnly(10, 0),
            RepeatOnMondays = true,
            ClientTimeZone = "UTC"
        });
        await Context.SaveChangesAsync();

        // Act
        var result = await service.GetRelevantTargets(carIds, currentDate, CancellationToken.None);

        // Assert
        // Should find next Monday (Oct 2, 2023) 10:00
        Assert.Single(result);
        Assert.Equal(2, result[0].NextExecutionTime.Day);
        Assert.Equal(10, result[0].NextExecutionTime.Hour);
    }
}
