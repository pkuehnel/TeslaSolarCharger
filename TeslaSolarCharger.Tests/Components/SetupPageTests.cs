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
using TeslaSolarCharger.Client.Components.Setup;
using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Pages;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Dtos.ChargingStation;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Dtos.Setup;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Localization;
using TeslaSolarCharger.Shared.TimeProviding;
using Xunit;

namespace TeslaSolarCharger.Tests.Components;

/// <summary>
/// Covers the routed setup shell: that an address lands on the screen it names, that the answers are saved as the
/// user moves, and that a failed save is reported as a failure instead of as a finished setup.
/// </summary>
public class SetupPageTests : Bunit.TestContext
{
    private readonly Mock<ISetupService> _setupService = new();
    private readonly Mock<ICloudConnectionCheckService> _cloudConnectionCheckService = new();
    private readonly Mock<IChargingStationsService> _chargingStationsService = new();
    private readonly Mock<ITemplateValueConfigurationService> _templateValueConfigurationService = new();
    private readonly Mock<ICarSettingsService> _carSettingsService = new();
    private readonly Mock<IHomeService> _homeService = new();
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
        _setupService.Setup(s => s.GetSetupState()).ReturnsAsync(() => _storedState);
        _setupService.Setup(s => s.UpdateSetupState(It.IsAny<DtoSetupState>())).Returns(Task.CompletedTask);
        _setupService.Setup(s => s.EvaluateSetupState(It.IsAny<DtoSetupState>())).ReturnsAsync(() => _decision);
        _setupService.Setup(s => s.SyncCarDrafts(It.IsAny<DtoSetupState>())).ReturnsAsync((DtoSetupState s) => s);
        _setupService
            .Setup(s => s.AcceptProposals(It.IsAny<DtoSetupState>(), It.IsAny<List<DtoSetupProposedValue>>()))
            .ReturnsAsync((DtoSetupState state, List<DtoSetupProposedValue> _) => state);
        _setupService
            .Setup(s => s.SaveCarDraft(It.IsAny<DtoSetupState>(), It.IsAny<Guid>()))
            .ReturnsAsync(new DtoSetupApplicationResult
            {
                Operations = { new DtoSetupOperationResult { OperationKey = SetupOperationKey.SaveCarDraft, IsSuccess = true, }, },
            });

        _cloudConnectionCheckService.Setup(s => s.GetBackendTokenState(It.IsAny<bool>())).ReturnsAsync(TokenState.UpToDate);
        _cloudConnectionCheckService.Setup(s => s.IsBaseAppLicensed(It.IsAny<bool>())).ReturnsAsync(true);
        _chargingStationsService.Setup(s => s.GetChargingStations()).ReturnsAsync(new List<DtoChargingStation>());
        _templateValueConfigurationService.Setup(s => s.GetOverviews()).ReturnsAsync(new List<DtoValueConfigurationOverview>());
        _carSettingsService.Setup(s => s.GetFleetApiTokenState()).ReturnsAsync(TokenState.UpToDate);
        _homeService.Setup(s => s.GetCarOverview(It.IsAny<int>())).ReturnsAsync(new DtoCarOverviewSettings("Car") { MinSoc = 20, });
        _httpClientHelper
            .Setup(h => h.SendGetRequestWithSnackbarAsync<List<DtoChargePrice>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DtoChargePrice>());

        Services.AddSingleton(_setupService.Object);
        Services.AddSingleton(_cloudConnectionCheckService.Object);
        Services.AddSingleton(_chargingStationsService.Object);
        Services.AddSingleton(_templateValueConfigurationService.Object);
        Services.AddSingleton(_carSettingsService.Object);
        Services.AddSingleton(_homeService.Object);
        Services.AddSingleton(_httpClientHelper.Object);
        Services.AddSingleton(Mock.Of<IChargePriceService>());
        Services.AddSingleton(Mock.Of<IOAuthNotificationService>());
        Services.AddSingleton(Mock.Of<IJavaScriptWrapper>());
    }

    private IRenderedComponent<Setup> RenderAt(string? section = null, Guid? draftId = null, string? stage = null) =>
        Render<Setup>(parameters =>
        {
            if (section != null)
            {
                parameters.Add(p => p.Section, section);
            }

            if (draftId != null)
            {
                parameters.Add(p => p.DraftId, draftId);
            }

            if (stage != null)
            {
                parameters.Add(p => p.Stage, stage);
            }
        });

    private static DtoSetupCarDraft CarDraft(string name = "Our car", SetupCarStage stage = SetupCarStage.Identify) => new()
    {
        CarId = 5,
        Stage = stage,
        ConnectionRoute = SetupCarConnectionRoute.TeslaBluetooth,
        Configuration = new CarBasicConfiguration
        {
            Id = 5, Name = name, Vin = "VIN1", UsableEnergy = 75, MaximumPhases = 3,
            MinimumAmpere = 6, MaximumAmpere = 16, ChargingPriority = 1, CarType = CarType.Tesla, UseBle = true,
            BleApiBaseUrl = "http://ble",
        },
    };

    [Fact]
    public void NoSectionInTheAddressStartsAtTheBeginning()
    {
        var page = RenderAt();

        Assert.Contains("Welcome to TeslaSolarCharger", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnAddressNamesTheScreenItShows()
    {
        var page = RenderAt(SetupSections.Prices);

        Assert.Contains("electricity", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SetupStepKey.Prices, LastSavedState().CurrentStep);
    }

    [Fact]
    public void TheEquipmentAddressShowsWhatTheUserHas()
    {
        _storedState = new DtoSetupState { CarDrafts = { CarDraft(), }, };

        var page = RenderAt(SetupSections.Equipment);

        Assert.Contains("Our car", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Add a car", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Add a charging station", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ACarAddressOpensThatCarAtThatStage()
    {
        var draft = CarDraft(stage: SetupCarStage.Connection);
        _storedState = new DtoSetupState { CarDrafts = { draft, }, };

        var page = RenderAt(SetupSections.Car, draft.DraftId, nameof(SetupCarStage.Connection));

        //The connection stage is the one that asks how the car should be reached.
        Assert.Contains("How should we reach this car", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Our car", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ACarThatIsNoLongerPartOfSetupSaysSoInsteadOfShowingAnEmptyForm()
    {
        _storedState = new DtoSetupState();

        var page = RenderAt(SetupSections.Car, Guid.NewGuid(), nameof(SetupCarStage.Identify));

        Assert.Contains("not part of your setup", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EachRouteSaysWhatControlsChargingAndWhatItCosts()
    {
        var draft = CarDraft(stage: SetupCarStage.Connection);
        draft.ConnectionRoute = SetupCarConnectionRoute.Undecided;
        draft.Configuration.CarType = CarType.Tesla;
        _storedState = new DtoSetupState { CarDrafts = { draft, }, };

        var page = RenderAt(SetupSections.Car, draft.DraftId, nameof(SetupCarStage.Connection));

        Assert.Contains("Charging is controlled through", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Battery level comes from", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Extra subscription for this car", page.Markup, StringComparison.OrdinalIgnoreCase);
        //The free route and the paid route are both named before either is chosen.
        Assert.Contains("none beyond the base licence", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("one subscription for this car", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ANonTeslaIsNotOfferedTheTeslaRoutes()
    {
        var draft = CarDraft(stage: SetupCarStage.Connection);
        draft.ConnectionRoute = SetupCarConnectionRoute.Undecided;
        draft.Make = "Hyundai";
        draft.Configuration.CarType = CarType.Manual;
        draft.Configuration.UseBle = false;
        _storedState = new DtoSetupState { CarDrafts = { draft, }, };

        var page = RenderAt(SetupSections.Car, draft.DraftId, nameof(SetupCarStage.Connection));

        Assert.Contains("Charging station only", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("through your Tesla account", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheChargerScreenBuildsTheAddressFromWhereTheUserIs()
    {
        var draft = new DtoSetupChargerDraft { ChargepointId = "GARAGE1", };
        _storedState = new DtoSetupState { ChargerDrafts = { draft, }, };

        var page = RenderAt(SetupSections.Charger, draft.DraftId, nameof(SetupChargerStage.Connect));

        //bUnit's browser sits on http://localhost/, which is the address a charger would have to call too.
        Assert.Contains("ws://localhost/api/Ocpp/GARAGE1", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void AChargerThatHasNotReportedInKeepsLookingRatherThanAskingTheUserToCheck()
    {
        var draft = new DtoSetupChargerDraft { ChargepointId = "GARAGE1", };
        _storedState = new DtoSetupState { ChargerDrafts = { draft, }, };

        var page = RenderAt(SetupSections.Charger, draft.DraftId, nameof(SetupChargerStage.Connect));

        Assert.Contains("Waiting for your charging station", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MovingToTheNextSectionSavesTheAnswersStraightAway()
    {
        _storedState = new DtoSetupState();
        var page = RenderAt(SetupSections.Welcome);

        ButtonWithText(page, "Next").Click();

        //Saving as the user goes is what lets an account authorization or a reload happen without losing answers.
        _setupService.Verify(s => s.UpdateSetupState(It.IsAny<DtoSetupState>()), Times.AtLeastOnce);
        Assert.Contains(SetupStepKey.Welcome, LastSavedState().CompletedSteps);
    }

    [Fact]
    public void AFailedSaveDoesNotFinishSetup()
    {
        _storedState = new DtoSetupState();
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

        var page = RenderAt(SetupSections.Finish);
        ButtonWithText(page, "Finish Setup").Click();

        //Nothing was completed, so the user must stay where they are with their answers intact.
        Assert.Empty(Services.GetRequiredService<BunitNavigationManager>().History);
    }

    [Fact]
    public void ASuccessfulFinishLeavesTheAssistant()
    {
        _storedState = new DtoSetupState();
        _setupService
            .Setup(s => s.ActivateAndCompleteSetup(It.IsAny<DtoSetupState>()))
            .ReturnsAsync(new DtoSetupApplicationResult { IsSetupCompleted = true, });

        var page = RenderAt(SetupSections.Finish);
        ButtonWithText(page, "Finish Setup").Click();

        Assert.Single(Services.GetRequiredService<BunitNavigationManager>().History);
    }

    [Fact]
    public void SavingWithoutEnablingDoesNotFinishSetupEither()
    {
        _storedState = new DtoSetupState();
        _setupService
            .Setup(s => s.ApplyConfiguration(It.IsAny<DtoSetupState>()))
            .ReturnsAsync(new DtoSetupApplicationResult
            {
                Operations = { new DtoSetupOperationResult { OperationKey = SetupOperationKey.SaveBaseConfiguration, IsSuccess = true, }, },
            });

        var page = RenderAt(SetupSections.Finish);
        ButtonWithText(page, "Save and enable later").Click();

        _setupService.Verify(s => s.ApplyConfiguration(It.IsAny<DtoSetupState>()), Times.Once);
        _setupService.Verify(s => s.ActivateAndCompleteSetup(It.IsAny<DtoSetupState>()), Times.Never);
        Assert.Empty(Services.GetRequiredService<BunitNavigationManager>().History);
    }

    [Fact]
    public void TheReviewSaysWhatWillHappenInPlainWords()
    {
        _storedState = new DtoSetupState { CarDrafts = { CarDraft(), }, HasHomeBattery = true, };
        _storedState.Configuration.DynamicHomeBatteryMinSoc = true;

        var page = RenderAt(SetupSections.Finish);

        Assert.Contains("Our car is controlled over Bluetooth", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("home battery keeps just enough charge", page.Markup, StringComparison.OrdinalIgnoreCase);
        //Configuration being complete and a charging test having been run are different things.
        Assert.Contains("No real charging test has been done yet", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProposalsAreShownBeforeTheyAreApplied()
    {
        _storedState = new DtoSetupState();
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

        var page = RenderAt(SetupSections.Finish);

        Assert.Contains("home battery", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WhatIsStillMissingIsSaidInPlainWords()
    {
        _storedState = new DtoSetupState();
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

        var page = RenderAt(SetupSections.Finish);

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

    private static AngleSharp.Dom.IElement ButtonWithText(IRenderedComponent<Setup> page, string text)
    {
        var buttons = page.FindAll("button")
            .Where(b => b.TextContent.Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.NotEmpty(buttons);
        return buttons.Last();
    }
}
