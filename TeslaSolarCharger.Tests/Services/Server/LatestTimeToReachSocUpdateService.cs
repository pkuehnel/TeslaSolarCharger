using Autofac;
using FakeItEasy;
using System;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.TimeProviding;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

public class LatestTimeToReachSocUpdateService : TestBase
{
    public LatestTimeToReachSocUpdateService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Theory, MemberData(nameof(CorrectData))]
    public void Correctly_Updates_LatestTimeToReachSoc(bool shouldIgnoreDate, DateTime currentDate, DateTime configuredDate, DateTime expectedDate)
    {
        var carConfiguration = new CarConfiguration()
        {
            IgnoreLatestTimeToReachSocDate = shouldIgnoreDate,
            LatestTimeToReachSoC = configuredDate,
        };

        _fake.Provide<IDateTimeProvider>(new FakeDateTimeProvider(currentDate));
        var latestTimeToReachSocUpdateService = _fake.Resolve<TeslaSolarCharger.Server.Services.LatestTimeToReachSocUpdateService>();
        latestTimeToReachSocUpdateService.UpdateCarConfiguration(carConfiguration);
        
        Assert.Equal(expectedDate, carConfiguration.LatestTimeToReachSoC);
    }

    public static readonly object[][] CorrectData =
    {
        new object[]
        {
            true,
            new DateTime(2023,2,2, 8, 0, 0),
            new DateTime(2023,1,15, 14, 0, 0),
            new DateTime(2023, 2, 2, 14, 0, 0),
        },
        new object[]
        {
            false,
            new DateTime(2023,2,2, 8, 0, 0),
            new DateTime(2023,1,15, 14, 0, 0),
            new DateTime(2023, 2, 1, 14, 0, 0),
        },
        new object[]
        {
            true,
            new DateTime(2023,2,2, 8, 0, 0),
            new DateTime(2023,2,15, 14, 0, 0),
            new DateTime(2023, 2, 2, 14, 0, 0),
        },
        new object[]
        {
            false,
            new DateTime(2023,2,2, 8, 0, 0),
            new DateTime(2023,2,15, 14, 0, 0),
            new DateTime(2023, 2, 15, 14, 0, 0),
        },
        new object[]
        {
            false,
            new DateTime(2023,2,16, 17, 0, 0),
            new DateTime(2023,2,17, 14, 0, 0),
            new DateTime(2023, 2, 17, 14, 0, 0),
        },
        new object[]
        {
            true,
            new DateTime(2023,2,2, 8, 0, 0),
            new DateTime(2023,2,2, 6, 0, 0),
            new DateTime(2023, 2, 3, 6, 0, 0),
        },
    };
}
