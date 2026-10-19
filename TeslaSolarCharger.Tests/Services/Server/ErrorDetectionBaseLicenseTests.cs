using Autofac;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.Shared.TimeProviding;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

/// <summary>
/// The error shown when Solar4Car has no base license or the license could not be checked.
/// </summary>
[SuppressMessage("ReSharper", "UseConfigureAwaitFalse")]
public class ErrorDetectionBaseLicenseTests(ITestOutputHelper outputHelper) : TestBase(outputHelper)
{
    private const string NotLicensedHeadline = "No Solar4Car base license";
    private const string CheckFailedHeadline = "Could not check if Solar4Car is licensed";

    private IIssueKeys IssueKeys => Mock.Create<IIssueKeys>();

    private void SetupLicense(bool? isLicensed, string? errorMessage = null) =>
        Mock.Mock<IBackendApiService>().Setup(b => b.IsBaseAppLicensed(true))
            .ReturnsAsync(new Result<bool?>(isLicensed, errorMessage, null));

    //The test database is seeded with other errors.
    private IQueryable<LoggedError> LicenseErrors() => Context.LoggedErrors.Where(e => e.IssueKey == IssueKeys.BaseAppNotLicensed);

    private async Task<List<LoggedError>> GetActiveErrors() =>
        await LicenseErrors().Where(e => e.EndTimeStamp == null).ToListAsync();

    private async Task Detect(bool backendTokenUpToDate = true) =>
        await Mock.Create<ErrorDetectionService>(new TypedParameter(typeof(IDateTimeProvider), new FakeDateTimeProvider(CurrentFakeDate.UtcDateTime)))
            .DetectBaseAppLicenseIssue(await GetActiveErrors(), backendTokenUpToDate);

    private async Task<LoggedError> AddOpenLicenseError(string headline, string message)
    {
        var error = new LoggedError
        {
            StartTimeStamp = CurrentFakeDate.UtcDateTime.AddHours(-1),
            IssueKey = IssueKeys.BaseAppNotLicensed,
            Source = nameof(ErrorHandlingService),
            MethodName = "AddOrRemoveErrors",
            Headline = headline,
            Message = message,
        };
        Context.LoggedErrors.Add(error);
        await Context.SaveChangesAsync();
        return error;
    }

    [Fact]
    public async Task NotLicensed_ExplainsWhatIsBlockedAndWhereToGetTheLicense()
    {
        SetupLicense(false);

        await Detect();

        var error = Assert.Single(await GetActiveErrors());
        Assert.Equal(IssueKeys.BaseAppNotLicensed, error.IssueKey);
        Assert.Equal(NotLicensedHeadline, error.Headline);
        Assert.Contains("does not control any charging", error.Message);
        Assert.Contains("BLE", error.Message);
        Assert.Contains($"href=\"{Mock.Create<IConstants>().SubscriptionsUrl}\"", error.Message);
        Assert.Contains("restart Solar4Car", error.Message);
    }

    [Fact]
    public async Task CheckFailed_ShowsTheBackendErrorMessage()
    {
        SetupLicense(null, "Backend not reachable");

        await Detect();

        var error = Assert.Single(await GetActiveErrors());
        Assert.Equal(CheckFailedHeadline, error.Headline);
        Assert.Equal("Backend not reachable", error.Message);
    }

    [Fact]
    public async Task CheckFailedWithoutErrorMessage_HasAnEmptyMessage()
    {
        SetupLicense(null);

        await Detect();

        var error = Assert.Single(await GetActiveErrors());
        Assert.Equal(CheckFailedHeadline, error.Headline);
        Assert.Equal(string.Empty, error.Message);
    }

    [Fact]
    public async Task Licensed_RaisesNoError()
    {
        SetupLicense(true);

        await Detect();

        Assert.Empty(await LicenseErrors().ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task BackendTokenNotUpToDate_RaisesNoError(bool? isLicensed)
    {
        SetupLicense(isLicensed);

        await Detect(backendTokenUpToDate: false);

        Assert.Empty(await LicenseErrors().ToListAsync());
    }

    [Fact]
    public async Task Licensed_ResolvesAnOpenError()
    {
        await AddOpenLicenseError(NotLicensedHeadline, "message");
        SetupLicense(true);

        await Detect();

        Assert.Empty(await GetActiveErrors());
        var error = Assert.Single(await LicenseErrors().ToListAsync());
        Assert.NotNull(error.EndTimeStamp);
    }

    /// <summary>
    /// Installations updated from an older version have an open error with the old headline and an empty message.
    /// </summary>
    [Fact]
    public async Task OpenErrorOfAnOlderVersion_GetsTheCurrentTexts()
    {
        var oldError = await AddOpenLicenseError("Base App not licensed", string.Empty);
        SetupLicense(false);

        await Detect();

        var error = Assert.Single(await GetActiveErrors());
        Assert.Equal(oldError.Id, error.Id);
        Assert.Equal(NotLicensedHeadline, error.Headline);
        Assert.Contains("does not control any charging", error.Message);
        Assert.Single(error.FurtherOccurrences);
    }

    [Fact]
    public async Task CheckFailedThenNotLicensed_HeadlineFollowsTheCurrentState()
    {
        SetupLicense(null, "Backend not reachable");
        await Detect();
        SetupLicense(false);

        await Detect();

        var error = Assert.Single(await GetActiveErrors());
        Assert.Equal(NotLicensedHeadline, error.Headline);
        Assert.DoesNotContain("Backend not reachable", error.Message);
    }
}
