using System.Linq;
using System.Reflection;
using TeslaSolarCharger.Server.Resources.PossibleIssues;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Shared.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

/// <summary>
/// Every issue key needs a registration: GetIssueByKey is used to list, filter and notify open errors, so one logged
/// error with an unregistered key made the start page's whole error list and the Telegram notifications fail. That is
/// what BleRadioSilence and BleAdapterNotFound did from v2.48.0 on.
/// </summary>
public class PossibleIssuesTests
{
    private static readonly IssueKeys Keys = new();

    public static TheoryData<string> AllIssueKeys()
    {
        var data = new TheoryData<string>();
        foreach (var property in typeof(IIssueKeys).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            data.Add((string)property.GetValue(Keys)!);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(AllIssueKeys))]
    public void EveryIssueKeyIsRegistered(string issueKey)
    {
        var possibleIssues = new PossibleIssues(Keys);
        Assert.NotNull(possibleIssues.GetIssueByKey(issueKey));
    }

    /// <summary>Prefix keys are logged with a suffix, which has to resolve to the prefix registration.</summary>
    [Theory]
    [MemberData(nameof(AllIssueKeys))]
    public void PrefixIssueKeysResolveWithASuffix(string issueKey)
    {
        if (!issueKey.EndsWith('_'))
        {
            return;
        }
        var possibleIssues = new PossibleIssues(Keys);
        Assert.Same(possibleIssues.GetIssueByKey(issueKey), possibleIssues.GetIssueByKey(issueKey + "FleetApiRequests/ChargeStop"));
    }

    [Fact]
    public void TheIssueKeysAreFound()
    {
        //Guards the reflection above: an empty theory would pass without checking anything.
        Assert.True(AllIssueKeys().Count() > 30);
    }

    [Fact]
    public void BleRadioSilenceIsAWarningShownImmediately()
    {
        var issue = new PossibleIssues(Keys).GetIssueByKey(Keys.BleRadioSilence);
        Assert.Equal(IssueSeverity.Warning, issue.IssueSeverity);
        Assert.Equal(1, issue.ShowErrorAfterOccurrences);
        Assert.True(issue.IsTelegramEnabled);
    }

    [Fact]
    public void BleAdapterNotFoundIsAnErrorShownImmediately()
    {
        var issue = new PossibleIssues(Keys).GetIssueByKey(Keys.BleAdapterNotFound);
        Assert.Equal(IssueSeverity.Error, issue.IssueSeverity);
        Assert.Equal(1, issue.ShowErrorAfterOccurrences);
        Assert.True(issue.IsTelegramEnabled);
    }
}
