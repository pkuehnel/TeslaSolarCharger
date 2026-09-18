using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Helper;
using TeslaSolarCharger.Shared.TimeProviding;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class BackendNotificationServiceTests : TestBase
{
    public BackendNotificationServiceTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    private BackendNotificationService CreateService(string? currentVersion)
    {
        var backendApiService = Mock.Mock<IBackendApiService>();
        backendApiService.Setup(s => s.GetCurrentVersion()).ReturnsAsync(currentVersion);
        return new(NullLogger<BackendNotificationService>.Instance,
            Context,
            new FakeDateTimeProvider(CurrentFakeDate.UtcDateTime),
            backendApiService.Object,
            new VersionHelper(NullLogger<VersionHelper>.Instance));
    }

    private async Task<int> AddNotification(string? validFromVersion = null, string? validToVersion = null,
        DateTime? validFromDate = null, DateTime? validToDate = null, bool isConfirmed = false)
    {
        var notification = new BackendNotification
        {
            BackendIssueId = 1001,
            Type = BackendNotificationType.Information,
            Headline = "Changes to chargemode on update",
            DetailText = "To make sure no cars will unexpectedly have an empty battery...",
            ValidFromDate = validFromDate,
            ValidToDate = validToDate,
            ValidFromVersion = validFromVersion,
            ValidToVersion = validToVersion,
            IsConfirmed = isConfirmed,
        };
        Context.BackendNotifications.Add(notification);
        await Context.SaveChangesAsync();
        DetachAllEntities();
        return notification.Id;
    }

    [Fact]
    public async Task ReturnsNotificationWithoutAnyRestrictions()
    {
        var id = await AddNotification();

        var notifications = await CreateService("2.48.1-alpha.3").GetRelevantBackendNotifications();

        Assert.Equal(id, Assert.Single(notifications).Id);
    }

    [Theory]
    //The alpha version is compared as 2.48.0 and is therefore newer than the upper bound
    [InlineData("2.48.1-alpha.3")]
    [InlineData("2.48.1")]
    [InlineData("2.37.3")]
    public async Task FiltersOutNotificationIfCurrentVersionIsNewerThanValidToVersion(string currentVersion)
    {
        await AddNotification(validToVersion: "2.37.2");

        var notifications = await CreateService(currentVersion).GetRelevantBackendNotifications();

        Assert.Empty(notifications);
    }

    [Theory]
    //The upper bound is inclusive
    [InlineData("2.37.2")]
    [InlineData("2.30.0")]
    public async Task ReturnsNotificationIfCurrentVersionIsNotNewerThanValidToVersion(string currentVersion)
    {
        var id = await AddNotification(validToVersion: "2.37.2");

        var notifications = await CreateService(currentVersion).GetRelevantBackendNotifications();

        Assert.Equal(id, Assert.Single(notifications).Id);
    }

    [Theory]
    [InlineData("2.37.1")]
    [InlineData("1.0.0")]
    public async Task FiltersOutNotificationIfCurrentVersionIsOlderThanValidFromVersion(string currentVersion)
    {
        await AddNotification(validFromVersion: "2.37.2");

        var notifications = await CreateService(currentVersion).GetRelevantBackendNotifications();

        Assert.Empty(notifications);
    }

    [Theory]
    //The lower bound is inclusive and a missing upper bound must not filter the notification out
    [InlineData("2.37.2")]
    [InlineData("2.48.1")]
    [InlineData("2.48.1-alpha.3")]
    public async Task ReturnsNotificationIfCurrentVersionIsNotOlderThanValidFromVersion(string currentVersion)
    {
        var id = await AddNotification(validFromVersion: "2.37.2");

        var notifications = await CreateService(currentVersion).GetRelevantBackendNotifications();

        Assert.Equal(id, Assert.Single(notifications).Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ReturnsNotificationIfCurrentVersionIsUnknown(string? currentVersion)
    {
        var id = await AddNotification(validToVersion: "2.37.2");

        var notifications = await CreateService(currentVersion).GetRelevantBackendNotifications();

        Assert.Equal(id, Assert.Single(notifications).Id);
    }

    [Fact]
    public async Task FiltersOutConfirmedNotifications()
    {
        await AddNotification(isConfirmed: true);

        var notifications = await CreateService("2.48.1-alpha.3").GetRelevantBackendNotifications();

        Assert.Empty(notifications);
    }

    [Fact]
    public async Task FiltersOutNotificationThatIsNotValidYet()
    {
        await AddNotification(validFromDate: CurrentFakeDate.UtcDateTime.AddDays(1));

        var notifications = await CreateService("2.48.1-alpha.3").GetRelevantBackendNotifications();

        Assert.Empty(notifications);
    }

    [Fact]
    public async Task FiltersOutNotificationThatIsNotValidAnymore()
    {
        await AddNotification(validToDate: CurrentFakeDate.UtcDateTime.AddDays(-1));

        var notifications = await CreateService("2.48.1-alpha.3").GetRelevantBackendNotifications();

        Assert.Empty(notifications);
    }

    [Fact]
    public async Task ReturnsNotificationWithinItsValidDateRange()
    {
        var id = await AddNotification(validFromDate: CurrentFakeDate.UtcDateTime.AddDays(-1),
            validToDate: CurrentFakeDate.UtcDateTime.AddDays(1));

        var notifications = await CreateService("2.48.1-alpha.3").GetRelevantBackendNotifications();

        Assert.Equal(id, Assert.Single(notifications).Id);
    }

    [Fact]
    public async Task ReturnsOnlyNotificationsRelevantForTheCurrentVersion()
    {
        var outdatedNotificationId = await AddNotification(validToVersion: "2.37.2");
        var currentNotificationId = await AddNotification(validFromVersion: "2.40.0");

        var notifications = await CreateService("2.48.1-alpha.3").GetRelevantBackendNotifications();

        Assert.Equal(currentNotificationId, Assert.Single(notifications).Id);
        Assert.DoesNotContain(notifications, n => n.Id == outdatedNotificationId);
    }

    [Fact]
    public async Task MarkBackendNotificationAsConfirmedRemovesItFromRelevantNotifications()
    {
        var id = await AddNotification();
        var service = CreateService("2.48.1-alpha.3");

        await service.MarkBackendNotificationAsConfirmed(id);

        Assert.True(Context.BackendNotifications.Single(n => n.Id == id).IsConfirmed);
        Assert.Empty(await CreateService("2.48.1-alpha.3").GetRelevantBackendNotifications());
    }

    [Fact]
    public async Task MarkBackendNotificationAsConfirmedThrowsForUnknownNotification()
    {
        var service = CreateService("2.48.1-alpha.3");

        await Assert.ThrowsAsync<ArgumentException>(() => service.MarkBackendNotificationAsConfirmed(42));
    }
}
