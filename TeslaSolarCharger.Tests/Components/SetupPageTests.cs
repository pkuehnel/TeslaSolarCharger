using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Pages;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Dtos.ChargingStation;
using TeslaSolarCharger.Shared.Dtos.Setup;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Localization;
using TeslaSolarCharger.Shared.TimeProviding;
using Xunit;

namespace TeslaSolarCharger.Tests.Components;

/// <summary>
/// Covers what the setup assistant does with the state it is given: resuming on the right step, and reporting a
/// failed save as a failure instead of finishing.
/// </summary>
public class SetupPageTests : Bunit.TestContext
{
    private readonly Mock<ISetupService> _setupService = new();
    private readonly Mock<ICloudConnectionCheckService> _cloudConnectionCheckService = new();
    private readonly Mock<IChargingStationsService> _chargingStationsService = new();
    private readonly Mock<ITemplateValueConfigurationService> _templateValueConfigurationService = new();
    private readonly Mock<IHttpClientHelper> _httpClientHelper = new();

    private DtoSetupState _storedState = new();
    private DtoSetupDecision _decision = new();

    public SetupPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSharedDependencies();
        Services.AddSingleton<IDateTimeProvider>(new FakeDateTimeProvider(new DateTime(2026, 9, 15, 8, 0, 0, DateTimeKind.Utc)));

        _setupService.Setup(s => s.GetOrCreateSetupState()).ReturnsAsync(() => _storedState);
        _setupService.Setup(s => s.UpdateSetupState(It.IsAny<DtoSetupState>())).Returns(Task.CompletedTask);
        _setupService.Setup(s => s.EvaluateSetupState(It.IsAny<DtoSetupState>())).ReturnsAsync(() => _decision);
        _setupService
            .Setup(s => s.AcceptProposals(It.IsAny<DtoSetupState>(), It.IsAny<List<DtoSetupProposedValue>>()))
            .ReturnsAsync((DtoSetupState state, List<DtoSetupProposedValue> _) => state);

        _cloudConnectionCheckService.Setup(s => s.GetBackendTokenState(It.IsAny<bool>())).ReturnsAsync(TokenState.UpToDate);
        _cloudConnectionCheckService.Setup(s => s.IsBaseAppLicensed(It.IsAny<bool>())).ReturnsAsync(true);
        _chargingStationsService.Setup(s => s.GetChargingStations()).ReturnsAsync(new List<DtoChargingStation>());
        _templateValueConfigurationService.Setup(s => s.GetOverviews()).ReturnsAsync(new List<DtoValueConfigurationOverview>());
        _httpClientHelper
            .Setup(h => h.SendGetRequestWithSnackbarAsync<List<DtoChargePrice>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DtoChargePrice>());

        Services.AddSingleton(_setupService.Object);
        Services.AddSingleton(_cloudConnectionCheckService.Object);
        Services.AddSingleton(_chargingStationsService.Object);
        Services.AddSingleton(_templateValueConfigurationService.Object);
        Services.AddSingleton(_httpClientHelper.Object);
        Services.AddSingleton(Mock.Of<ICarSettingsService>());
        Services.AddSingleton(Mock.Of<IChargePriceService>());
        Services.AddSingleton(Mock.Of<IOAuthNotificationService>());
    }

    private IRenderedComponent<Setup> RenderSetupPage() => Render<Setup>();

    [Fact]
    public void ResumesOnTheStepTheUserLeftRatherThanAtTheStart()
    {
        _storedState = new DtoSetupState { CurrentStep = SetupStepKey.Location, };

        var page = RenderSetupPage();

        //The state is stored by key while the stepper works in positions, so this proves the two line up: the
        //location step's own text is on screen.
        Assert.Contains("charge", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mud-stepper", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SetupStepKey.Location, ActiveStepKey(page));
    }

    [Fact]
    public void AStepKeyThisBuildDoesNotKnowDoesNotStrandTheUser()
    {
        _storedState = new DtoSetupState { CurrentStep = SetupStepKey.Unknown, };

        var page = RenderSetupPage();

        Assert.Equal(SetupStepKey.Welcome, ActiveStepKey(page));
    }

    [Fact]
    public void MovingToAnotherStepSavesTheAnswersStraightAway()
    {
        _storedState = new DtoSetupState { CurrentStep = SetupStepKey.Welcome, };
        var page = RenderSetupPage();

        ClickNext(page);

        //Saving as the user goes is what lets an account authorization or a reload happen without losing answers.
        _setupService.Verify(s => s.UpdateSetupState(It.IsAny<DtoSetupState>()), Times.AtLeastOnce);
        Assert.Equal(SetupStepKey.CloudConnection, LastSavedState().CurrentStep);
    }

    [Fact]
    public void AFailedSaveDoesNotFinishSetup()
    {
        _storedState = new DtoSetupState { CurrentStep = SetupStepKey.Finish, };
        _setupService
            .Setup(s => s.ActivateAndCompleteSetup(It.IsAny<DtoSetupState>()))
            .ReturnsAsync(new DtoSetupApplicationResult
            {
                Operations =
                {
                    new DtoSetupOperationResult
                    {
                        OperationKey = SetupOperationKey.SaveCarDraft, IsSuccess = false, ErrorMessage = "car rejected",
                    },
                },
            });

        var page = RenderSetupPage();
        ClickFinish(page);

        //Nothing was completed, so the user must stay where they are with their answers intact.
        Assert.Empty(Services.GetRequiredService<BunitNavigationManager>().History);
    }

    [Fact]
    public void ASuccessfulFinishLeavesTheAssistant()
    {
        _storedState = new DtoSetupState { CurrentStep = SetupStepKey.Finish, };
        _setupService
            .Setup(s => s.ActivateAndCompleteSetup(It.IsAny<DtoSetupState>()))
            .ReturnsAsync(new DtoSetupApplicationResult { IsSetupCompleted = true, });

        var page = RenderSetupPage();
        ClickFinish(page);

        Assert.Single(Services.GetRequiredService<BunitNavigationManager>().History);
    }

    [Fact]
    public void SavingWithoutEnablingDoesNotFinishSetupEither()
    {
        _storedState = new DtoSetupState { CurrentStep = SetupStepKey.Finish, };
        _setupService
            .Setup(s => s.ApplyConfiguration(It.IsAny<DtoSetupState>()))
            .ReturnsAsync(new DtoSetupApplicationResult
            {
                Operations = { new DtoSetupOperationResult { OperationKey = SetupOperationKey.SaveBaseConfiguration, IsSuccess = true, }, },
            });

        var page = RenderSetupPage();
        ButtonWithText(page, "Save and enable later").Click();

        _setupService.Verify(s => s.ApplyConfiguration(It.IsAny<DtoSetupState>()), Times.Once);
        _setupService.Verify(s => s.ActivateAndCompleteSetup(It.IsAny<DtoSetupState>()), Times.Never);
        Assert.Empty(Services.GetRequiredService<BunitNavigationManager>().History);
    }

    [Fact]
    public void ProposalsAreShownBeforeTheyAreApplied()
    {
        _storedState = new DtoSetupState { CurrentStep = SetupStepKey.Finish, };
        _decision = new DtoSetupDecision
        {
            IsConfigurationComplete = true,
            ProposedValues =
            {
                new DtoSetupProposedValue
                {
                    PropertyName = nameof(BaseConfigurationBase.DynamicHomeBatteryMinSoc),
                    Value = true,
                    ReasonKey = TranslationKeys.SetupReasonDynamicHomeBatteryMinSoc,
                },
            },
        };

        var page = RenderSetupPage();

        Assert.Contains("home battery", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WhatIsStillMissingIsSaidInPlainWords()
    {
        _storedState = new DtoSetupState { CurrentStep = SetupStepKey.Finish, };
        _decision = new DtoSetupDecision
        {
            MissingInformation =
            {
                new DtoSetupIssue
                {
                    Severity = SetupIssueSeverity.MissingInformation,
                    MessageKey = TranslationKeys.SetupIssueHomeBatteryCapacityUnknown,
                    StepKey = SetupStepKey.SolarAndBattery,
                    //The internal name is carried for diagnostics only and must not reach the screen.
                    PropertyName = nameof(BaseConfigurationBase.HomeBatteryUsableEnergy),
                },
            },
        };

        var page = RenderSetupPage();

        Assert.Contains("usable capacity", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nameof(BaseConfigurationBase.HomeBatteryUsableEnergy), page.Markup, StringComparison.Ordinal);
    }

    /// <summary>The state as the page last handed it to the server, which is what a resume would read back.</summary>
    private DtoSetupState LastSavedState()
    {
        var invocations = _setupService.Invocations
            .Where(i => i.Method.Name == nameof(ISetupService.UpdateSetupState))
            .ToList();
        Assert.NotEmpty(invocations);
        return (DtoSetupState)invocations.Last().Arguments[0];
    }

    /// <summary>
    /// Which step the stepper is actually showing, read back from the step header the stepper marks as active.
    /// </summary>
    private static SetupStepKey ActiveStepKey(IRenderedComponent<Setup> page)
    {
        var activeLabel = page.FindAll(".mud-step.active .mud-step-label-content-title")
            .Select(e => e.TextContent.Trim())
            .FirstOrDefault()
            ?? page.FindAll(".mud-step.active").Select(e => e.TextContent.Trim()).FirstOrDefault();
        Assert.NotNull(activeLabel);

        //The step titles as the English registry spells them.
        return activeLabel switch
        {
            var t when t!.Contains("Welcome", StringComparison.OrdinalIgnoreCase) => SetupStepKey.Welcome,
            var t when t.Contains("Account", StringComparison.OrdinalIgnoreCase)
                       || t.Contains("Cloud", StringComparison.OrdinalIgnoreCase) => SetupStepKey.CloudConnection,
            var t when t.Contains("Solar", StringComparison.OrdinalIgnoreCase) => SetupStepKey.SolarAndBattery,
            var t when t.Contains("Location", StringComparison.OrdinalIgnoreCase) => SetupStepKey.Location,
            var t when t.Contains("Price", StringComparison.OrdinalIgnoreCase) => SetupStepKey.Prices,
            var t when t.Contains("Car", StringComparison.OrdinalIgnoreCase) => SetupStepKey.CarsAndCharging,
            var t when t.Contains("Finish", StringComparison.OrdinalIgnoreCase) => SetupStepKey.Finish,
            _ => SetupStepKey.Unknown,
        };
    }

    private static void ClickFinish(IRenderedComponent<Setup> page) => ButtonWithText(page, "Finish Setup").Click();

    private static void ClickNext(IRenderedComponent<Setup> page) => ButtonWithText(page, "Next").Click();

    private static AngleSharp.Dom.IElement ButtonWithText(IRenderedComponent<Setup> page, string text)
    {
        var buttons = page.FindAll("button")
            .Where(b => b.TextContent.Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.NotEmpty(buttons);
        //The stepper repeats a step's title in its header, so take the action button, which is the last one.
        return buttons.Last();
    }
}
