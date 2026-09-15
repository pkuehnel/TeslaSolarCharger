using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using MudExtensions.Services;
using TeslaSolarCharger.Client.Components;
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

    //An installation the server considers fully described. Finishing switches equipment on, so the button is only
    //offered once the server says everything required is there - tests that need it blocked say so explicitly.
    private DtoSetupDecision _decision = new() { IsConfigurationComplete = true, };

    public SetupPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddMudExtensions();
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
        //The advanced value-source editors reach for a raw HttpClient while rendering. They are not what these
        //tests are about, so they get an empty answer rather than a failure.
        Services.AddSingleton(new HttpClient(new EmptyJsonHandler()) { BaseAddress = new Uri("http://localhost/"), });
    }

    /// <summary>Answers every request with an empty JSON list, so a component that lists things renders nothing.</summary>
    private sealed class EmptyJsonHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json"),
            });
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
    public void TheSolarScreenAsksForTheEquipmentByName()
    {
        _storedState = new DtoSetupState { HasPvSystem = true, };

        var page = RenderAt(SetupSections.Solar);

        //Asked by the name on the box. The protocols still exist, but behind a door labelled by the problem the
        //user has ("my device is not in the list") rather than by the protocol names themselves.
        Assert.Contains("Make and model", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("My device is not in the list", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AMeasurementNobodySuppliesIsNamedRatherThanLeftBlank()
    {
        _storedState = new DtoSetupState { HasPvSystem = true, };

        var page = RenderAt(SetupSections.Solar);

        Assert.Contains("Electricity to and from the grid", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No device supplies this yet", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AHouseWithoutABatteryIsNotAskedForBatteryReadings()
    {
        _storedState = new DtoSetupState { HasPvSystem = true, HasHomeBattery = false, };

        var page = RenderAt(SetupSections.Solar);

        //A gap that can never be closed is not a gap worth showing.
        Assert.DoesNotContain("Home battery level", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ABatteryWithoutSolarPanelsIsStillSetUp()
    {
        _storedState = new DtoSetupState { HasPvSystem = false, HasHomeBattery = true, };

        var page = RenderAt(SetupSections.Solar);

        Assert.Contains("Home battery level", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("How much to keep back", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkingOutTheReserveIsKeptApartFromCommandingTheBattery()
    {
        _storedState = new DtoSetupState { HasHomeBattery = true, };

        var page = RenderAt(SetupSections.Solar);

        //Setup does not switch battery control on, and says so rather than leaving the user to assume either way.
        Assert.Contains("Telling the battery when to charge", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("leaves that switched off", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheAutomaticReserveSaysWhatItIsWaitingFor()
    {
        _storedState = new DtoSetupState { HasHomeBattery = true, };
        _storedState.Configuration.DynamicHomeBatteryMinSoc = true;
        _decision = new DtoSetupDecision
        {
            ProposedValues =
            {
                new DtoSetupProposedValue
                {
                    PropertyName = nameof(BaseConfigurationBase.DynamicHomeBatteryMinSoc),
                    Value = true,
                    IsPending = true,
                    PendingReasons =
                    {
                        new DtoSetupIssue { MessageKey = TranslationKeys.SetupIssueHomeBatteryCapacityUnknown, },
                    },
                },
            },
        };

        var page = RenderAt(SetupSections.Solar);

        //The choice stays as the user left it and is reported as waiting, not quietly turned back into a number.
        Assert.Contains("as soon as we know", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("usable capacity", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThePriceScreenAsksOneQuestionBeforeShowingAnyForm()
    {
        _storedState = new DtoSetupState();

        var page = RenderAt(SetupSections.Prices);

        Assert.Contains("stay the same, change at set times, or follow market prices", page.Markup, StringComparison.OrdinalIgnoreCase);
        //Nothing is filled in until the question is answered, so no market or time-of-use form is on screen.
        Assert.DoesNotContain("Your market price contract", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("When the price is different", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AMarketContractShowsOnlyTheMarketForm()
    {
        _storedState = new DtoSetupState { ElectricityPriceKind = SetupElectricityPriceKind.Market, };

        var page = RenderAt(SetupSections.Prices);

        Assert.Contains("Your market price contract", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("When the price is different", page.Markup, StringComparison.OrdinalIgnoreCase);
        //The starting markup is an example, and saying so is what stops it being taken for the user's contract.
        Assert.Contains("only an example", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ATimeOfUseContractShowsOnlyTheTimeForm()
    {
        _storedState = new DtoSetupState { ElectricityPriceKind = SetupElectricityPriceKind.TimeOfUse, };

        var page = RenderAt(SetupSections.Prices);

        Assert.Contains("When the price is different", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Your market price contract", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WithoutSolarPanelsThereIsNoExportPriceToAskFor()
    {
        _storedState = new DtoSetupState { ElectricityPriceKind = SetupElectricityPriceKind.Fixed, HasPvSystem = false, };

        var page = RenderAt(SetupSections.Prices);

        Assert.DoesNotContain("What your own solar electricity is worth", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WithSolarPanelsTheExportPriceIsExplainedRatherThanJustAsked()
    {
        _storedState = new DtoSetupState { ElectricityPriceKind = SetupElectricityPriceKind.Fixed, HasPvSystem = true, };

        var page = RenderAt(SetupSections.Prices);

        Assert.Contains("What your own solar electricity is worth", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("electricity you do not sell", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnInstallationConfiguredBeforeThisQuestionExistedIsNotAskedAgain()
    {
        //No stored answer, but the price already says it follows the market. Read back rather than asked again.
        _storedState = new DtoSetupState
        {
            ChargePrice = new DtoChargePrice { GridPrice = 0.31m, AddSpotPriceToGridPrice = true, },
        };

        var page = RenderAt(SetupSections.Prices);

        Assert.Contains("Your market price contract", page.Markup, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void FinishingIsNotOfferedWhileSomethingIsStillMissing()
    {
        //The button used to be live whenever the page was not busy, so a car with no capacity or phases could be
        //switched on and setup marked finished around it.
        _storedState = new DtoSetupState();
        _decision = new DtoSetupDecision
        {
            IsConfigurationComplete = false,
            MissingInformation =
            {
                new DtoSetupIssue
                {
                    Severity = SetupIssueSeverity.MissingInformation,
                    MessageKey = TranslationKeys.SetupIssueCarUsableEnergyUnknown,
                    StepKey = SetupStepKey.CarsAndCharging,
                },
            },
        };

        var page = RenderAt(SetupSections.Finish);

        Assert.True(ButtonWithText(page, "Finish Setup").HasAttribute("disabled"));
        //Saving without enabling changes nothing about how cars charge, so it stays available.
        Assert.False(ButtonWithText(page, "Save and enable later").HasAttribute("disabled"));
    }

    [Fact]
    public void AnUnansweredTariffStartsEmptyRatherThanAtAnExampleNumber()
    {
        //A plausible looking price that was never the user's own quietly makes every charging decision wrong, and
        //nothing would prompt them to look at it again.
        _storedState = new DtoSetupState();

        var page = RenderAt(SetupSections.Prices);

        Assert.DoesNotContain("0.285", page.Markup);
        Assert.DoesNotContain("0,285", page.Markup);
    }

    [Fact]
    public void TheBrowserKeepsBothIdsAfterTheServerCreatesTheCar()
    {
        //The configuration's own id is what reaches the runtime car on the next save. Copying only the draft id
        //left a zero there, which renumbered a car that was already running.
        var draft = new DtoSetupCarDraft
        {
            CarId = null,
            Stage = SetupCarStage.Identify,
            ConnectionRoute = SetupCarConnectionRoute.ChargingStationOnly,
            Configuration = new CarBasicConfiguration { Id = 0, Name = "New car", Vin = "NEWVIN", },
        };
        _storedState = new DtoSetupState { CarDrafts = { draft, }, };
        // A separate object, the way the server's answer really arrives: the page must copy both ids out of it
        // rather than happening to share one in-memory draft with the server.
        _setupService.Setup(s => s.GetSetupState()).ReturnsAsync(() => new DtoSetupState
        {
            CarDrafts =
            {
                new DtoSetupCarDraft
                {
                    DraftId = draft.DraftId,
                    CarId = 11,
                    Configuration = new CarBasicConfiguration { Id = 11, Name = "New car", Vin = "NEWVIN", },
                },
            },
        });

        var page = RenderAt(SetupSections.Car, draft.DraftId, SetupCarStage.Identify.ToString());
        // Naming the car is what gives it a row; the stage saves as soon as it can tell the car apart.
        page.FindAll("input")[2].Change("Renamed car");

        Assert.Equal(11, draft.CarId);
        Assert.Equal(11, draft.Configuration.Id);
    }

    [Fact]
    public void ChangingOnlyATariffPeriodsDaysIsStoredStraightAway()
    {
        //The price callbacks were wired up, but the day and time fields of a period were not, so an edit that only
        //moved a period was lost on the next reload.
        _storedState = new DtoSetupState
        {
            ElectricityPriceKind = SetupElectricityPriceKind.TimeOfUse,
            CurrentStep = SetupStepKey.Prices,
        };
        _httpClientHelper
            .Setup(h => h.SendGetRequestWithSnackbarAsync<List<DtoChargePrice>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DtoChargePrice>
            {
                new()
                {
                    Id = 1, GridPrice = 0.3m, SolarPrice = 0.1m, ValidSince = new DateTime(2026, 1, 1),
                    EnergyProviderConfiguration = "[{\"FromHour\":22,\"ToHour\":6,\"Value\":0.19}]",
                },
            });

        var page = RenderAt(SetupSections.Prices);
        var savesBefore = _setupService.Invocations.Count(i => i.Method.Name == nameof(ISetupService.UpdateSetupState));
        var mondayBox = page.FindComponents<MudCheckBox<bool>>().First();
        page.InvokeAsync(() => mondayBox.Instance.ValueChanged.InvokeAsync(false)).GetAwaiter().GetResult();

        Assert.True(_setupService.Invocations.Count(i => i.Method.Name == nameof(ISetupService.UpdateSetupState)) > savesBefore);
        //And the change itself is real: the period now names the days it covers, minus the one just unticked.
        var period = page.FindComponent<FixedPriceComponent>().Instance;
        Assert.NotNull(period.FixedPrice!.ValidOnDays);
        Assert.DoesNotContain(DayOfWeek.Sunday, period.FixedPrice.ValidOnDays!);
    }

    [Fact]
    public void ANewTariffPeriodShowsTheDaysItActuallyCovers()
    {
        //A period with no day list applies every day, which is how the price is worked out. The editor showed it
        //as covering no days, and ticking one did nothing at all.
        _storedState = new DtoSetupState
        {
            ElectricityPriceKind = SetupElectricityPriceKind.TimeOfUse,
            CurrentStep = SetupStepKey.Prices,
        };
        _httpClientHelper
            .Setup(h => h.SendGetRequestWithSnackbarAsync<List<DtoChargePrice>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DtoChargePrice>
            {
                new()
                {
                    Id = 1, GridPrice = 0.3m, SolarPrice = 0.1m, ValidSince = new DateTime(2026, 1, 1),
                    EnergyProviderConfiguration = "[{\"FromHour\":22,\"ToHour\":6,\"Value\":0.19}]",
                },
            });

        var page = RenderAt(SetupSections.Prices);
        var dayBoxes = page.FindAll("input[type=checkbox]").Take(7).ToList();

        Assert.Equal(7, dayBoxes.Count);
        Assert.All(dayBoxes, box => Assert.True(box.HasAttribute("checked")));
    }

    [Fact]
    public void TheReviewDescribesTheBatteryReserveThatWillActuallyApply()
    {
        //The recommendation is applied on the way to finishing. Describing the stored value instead told the user
        //their battery stays manual while a ticked box on the same screen said it was about to become automatic.
        _storedState = new DtoSetupState { HasHomeBattery = true, };
        _storedState.Configuration.DynamicHomeBatteryMinSoc = null;
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

        Assert.DoesNotContain("reserve you set by hand", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DecliningAnAutomationSurvivesAReload()
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

        //Untick the recommendation.
        page.FindAll("input[type=checkbox]").Last().Change(false);

        //Kept with the rest of the answers, not only in the browser: a reload used to accept it again silently.
        Assert.Contains($"|{nameof(BaseConfigurationBase.DynamicHomeBatteryMinSoc)}",
            Assert.Single(LastSavedState().DeclinedProposalIds));
    }
}
