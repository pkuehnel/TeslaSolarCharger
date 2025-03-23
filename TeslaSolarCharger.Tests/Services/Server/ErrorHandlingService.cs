using LanguageExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Shared.Dtos.LoggedError;
using TeslaSolarCharger.Shared.Enums;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

public class ErrorHandlingService(ITestOutputHelper outputHelper) : TestBase(outputHelper)
{
    [Fact]
    public async Task CanGetActiveErrors()
    {
        var errorHandlingService = Mock.Create<TeslaSolarCharger.Server.Services.ErrorHandlingService>();
        var unfilteredErrors = await errorHandlingService.GetActiveLoggedErrors();
        Assert.Equal(2, unfilteredErrors.Count);
        Assert.Single(unfilteredErrors, e => e.Id == -1);
        Assert.Single(unfilteredErrors, e => e.Id == -2);

    }

    [Fact]
    public async Task CanGetHiddenErrors()
    {
        var errorHandlingService = Mock.Create<TeslaSolarCharger.Server.Services.ErrorHandlingService>();
        var unfilteredErrors = await errorHandlingService.GetHiddenErrors();
        Assert.Equal(2, unfilteredErrors.Count);
        Assert.Single(unfilteredErrors, e => e.Id == -3);
        Assert.Single(unfilteredErrors, e => e.Id == -4);
        var notEnoughOccurrencesElement = unfilteredErrors.Single(e => e.Id == -3);
        Assert.Equal(LoggedErrorHideReason.NotEnoughOccurrences, notEnoughOccurrencesElement.HideReason);

        var dismissedElement = unfilteredErrors.Single(e => e.Id == -4);
        Assert.Equal(LoggedErrorHideReason.Dismissed, dismissedElement.HideReason);
    }
}
