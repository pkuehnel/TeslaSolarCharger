using System;
using System.Collections.Generic;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.TimeProviding;
using Xunit;
using Xunit.Abstractions;
using static MudBlazor.FilterOperator;

namespace TeslaSolarCharger.Tests.Services.Server;

public class ChargingServiceV2 : TestBase
{
    public ChargingServiceV2(ITestOutputHelper outputHelper)
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

}
