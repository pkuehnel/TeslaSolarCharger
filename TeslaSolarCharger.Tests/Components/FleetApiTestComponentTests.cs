using System;
using System.Collections.Generic;
using System.Linq;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using MudExtensions.Services;
using TeslaSolarCharger.Client.Components.StartPage;
using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Components;

/// <summary>
/// The Tesla cloud key as the user meets it in setup, in the car dialog and on the home page: first find out whether
/// the car already has the key, and only if not, walk the user through adding it and testing it again.
/// </summary>
public class FleetApiTestComponentTests : Bunit.TestContext
{
    private const int CarId = 5;
    private const string TeslaKeyUrl = "https://tesla.com/_ak/solar4car.com";
    private const string AddKeyText = "Add key";
    private const string TestText = "Test connection";
    //A failed first try is repeated after a second, which is longer than bUnit waits by default.
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    private readonly Mock<IHomeService> _homeService = new();
    private readonly List<(bool IsSuccess, string? Message)> _reportedTests = new();

    public FleetApiTestComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddMudExtensions();
        Services.AddSharedDependencies();
        Services.AddSingleton(_homeService.Object);

        StoredState(TeslaCarFleetApiState.NotConfigured);
        TestAnswers(false);
        _homeService.Setup(s => s.UpdateCarFleetApiState(It.IsAny<int>(), It.IsAny<TeslaCarFleetApiState>()))
            .ReturnsAsync(new Result<object?>(null, null, null));
    }

    private void StoredState(TeslaCarFleetApiState? state, string? errorMessage = null) =>
        _homeService.Setup(s => s.GetFleetApiState(It.IsAny<int>()))
            .ReturnsAsync(new Result<TeslaCarFleetApiState?>(state, errorMessage, null));

    /// <summary>What the car answers, one value per test request in that order; the last one repeats.</summary>
    private void TestAnswers(params bool[] answers)
    {
        var queue = new Queue<bool>(answers);
        _homeService.Setup(s => s.TestFleetApiAccess(It.IsAny<int>()))
            .ReturnsAsync(() => new Result<bool>(queue.Count > 1 ? queue.Dequeue() : queue.Peek(), null, null));
    }

    private int TestRequests() => _homeService.Invocations.Count(i => i.Method.Name == nameof(IHomeService.TestFleetApiAccess));

    private IRenderedComponent<FleetApiTestComponent> RenderComponent(bool autoTest = false, bool isOptional = false, int? carId = CarId) =>
        Render<FleetApiTestComponent>(parameters => parameters
            .Add(p => p.CarId, carId)
            .Add(p => p.AutoTest, autoTest)
            .Add(p => p.IsOptional, isOptional)
            .Add(p => p.OnTested, EventCallback.Factory.Create<(bool IsSuccess, string? Message)>(this, result => _reportedTests.Add(result))));

    private static IRenderedComponent<MudButton> Button(IRenderedComponent<FleetApiTestComponent> component, string text) =>
        component.FindComponents<MudButton>().Single(b => b.Find("a, button").TextContent.Contains(text, StringComparison.Ordinal));

    private static bool IsNextStep(IRenderedComponent<FleetApiTestComponent> component, string buttonText) =>
        Button(component, buttonText).Instance.Variant == Variant.Filled;

    [Fact]
    public void AStoredMissingKeyIsStillTestedFirst()
    {
        //A new installation, or one moved to another device, stores "no key" although the car may have had the
        //key for years. Only the test can tell.
        TestAnswers(true);

        var component = RenderComponent(autoTest: true);

        component.WaitForAssertion(() => Assert.Contains("The connection via the Tesla cloud works.", component.Markup, StringComparison.Ordinal), TestTimeout);
        Assert.Equal(1, TestRequests());
        Assert.Empty(component.FindComponents<MudButton>());
    }

    [Fact]
    public void AFailedTestWalksTheUserThroughAddingTheKey()
    {
        var component = RenderComponent(autoTest: true);

        component.WaitForAssertion(() => Assert.Contains("does not work yet", component.Markup, StringComparison.Ordinal), TestTimeout);
        Assert.Contains("1. Add the Solar4Car key to your car", component.Markup, StringComparison.Ordinal);
        Assert.Contains("2. Test the connection", component.Markup, StringComparison.Ordinal);
        Assert.True(IsNextStep(component, AddKeyText));
        Assert.False(IsNextStep(component, TestText));
        Assert.Contains("Controls -&gt; Locks", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void TheKeyIsAddedOnTeslasOwnPageInANewTab()
    {
        var component = RenderComponent();

        var link = Button(component, AddKeyText).Find("a");

        Assert.Equal(TeslaKeyUrl, link.GetAttribute("href"));
        Assert.Equal("_blank", link.GetAttribute("target"));
    }

    [Fact]
    public void AFailedFirstTryIsRetriedOnceBeforeItCounts()
    {
        //The first request can fail only because the car turns out to need the command proxy, which is learnt from
        //that very failure.
        TestAnswers(false, true);

        var component = RenderComponent(autoTest: true);

        component.WaitForAssertion(() => Assert.Contains("The connection via the Tesla cloud works.", component.Markup, StringComparison.Ordinal), TestTimeout);
        Assert.Equal(2, TestRequests());
    }

    [Fact]
    public void AKnownWorkingConnectionIsNotTestedAgain()
    {
        StoredState(TeslaCarFleetApiState.Ok);

        var component = RenderComponent(autoTest: true);

        //Guided flows still say so: the user is waiting for the answer.
        component.WaitForAssertion(() => Assert.Contains("The connection via the Tesla cloud works.", component.Markup, StringComparison.Ordinal), TestTimeout);
        Assert.Equal(0, TestRequests());
    }

    [Fact]
    public void TheHomePageShowsNothingForAWorkingConnection()
    {
        StoredState(TeslaCarFleetApiState.Ok);

        var component = RenderComponent();

        Assert.Empty(component.FindComponents<MudAlert>());
        Assert.Empty(component.FindComponents<MudButton>());
    }

    [Fact]
    public void WithoutAutoTestNothingIsTestedUntilTheUserAsks()
    {
        var component = RenderComponent();

        Assert.Contains("Solar4Car is not registered in this car yet.", component.Markup, StringComparison.Ordinal);
        Assert.True(IsNextStep(component, AddKeyText));
        Assert.Equal(0, TestRequests());
    }

    [Fact]
    public void AddingTheKeyIsRememberedAndMakesTheTestTheNextStep()
    {
        var component = RenderComponent();

        Button(component, AddKeyText).Find("a").Click();

        _homeService.Verify(s => s.UpdateCarFleetApiState(CarId, TeslaCarFleetApiState.OpenedLinkButNotTested), Times.Once);
        Assert.Contains("You added the key but have not tested the connection yet.", component.Markup, StringComparison.Ordinal);
        Assert.True(IsNextStep(component, TestText));
        Assert.False(IsNextStep(component, AddKeyText));
        Assert.Equal(0, TestRequests());
    }

    [Theory]
    [InlineData(TeslaCarFleetApiState.OpenedLinkButNotTested, "You added the key but have not tested the connection yet.")]
    [InlineData(null, "The connection via the Tesla cloud has not been tested yet.")]
    public void AKeyThatMayBeThereMakesTheTestTheNextStep(TeslaCarFleetApiState? state, string expectedStatus)
    {
        StoredState(state);

        var component = RenderComponent();

        Assert.Contains(expectedStatus, component.Markup, StringComparison.Ordinal);
        Assert.True(IsNextStep(component, TestText));
    }

    [Fact]
    public void AKeyTheCarRejectedMakesAddingItTheNextStep()
    {
        StoredState(TeslaCarFleetApiState.NotWorking);

        var component = RenderComponent();

        Assert.Contains("The car did not accept Solar4Car the last time we tried.", component.Markup, StringComparison.Ordinal);
        Assert.True(IsNextStep(component, AddKeyText));
    }

    [Fact]
    public void ASuccessfulTestIsReportedToTheGuidedFlow()
    {
        TestAnswers(true);
        var component = RenderComponent();

        Button(component, TestText).Find("button").Click();

        component.WaitForAssertion(() => Assert.Equal(new[] { (true, (string?)null), }, _reportedTests), TestTimeout);
        Assert.Contains("The connection via the Tesla cloud works.", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void AFailedTestIsReportedToTheGuidedFlow()
    {
        var component = RenderComponent(autoTest: true);

        component.WaitForAssertion(() => Assert.Single(_reportedTests), TestTimeout);
        Assert.False(_reportedTests[0].IsSuccess);
        Assert.Equal("The connection via the Tesla cloud does not work yet.", _reportedTests[0].Message);
    }

    [Fact]
    public void ARequestThatFailsCountsAsAFailedTest()
    {
        _homeService.Setup(s => s.TestFleetApiAccess(It.IsAny<int>()))
            .ReturnsAsync(new Result<bool>(false, "Network error", null));

        var component = RenderComponent(autoTest: true);

        component.WaitForAssertion(() => Assert.Contains("does not work yet", component.Markup, StringComparison.Ordinal), TestTimeout);
        Assert.True(IsNextStep(component, AddKeyText));
    }

    [Fact]
    public void AMissingKeyIsAnErrorForACarThatNeedsTheTeslaCloud()
    {
        var component = RenderComponent();

        Assert.Equal(Severity.Error, component.FindComponent<MudAlert>().Instance.Severity);
        Assert.DoesNotContain("Recommended, but optional", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingKeyIsARecommendationForABluetoothCar()
    {
        var component = RenderComponent(isOptional: true);

        Assert.Equal(Severity.Info, component.FindComponent<MudAlert>().Instance.Severity);
        Assert.Contains("Recommended, but optional", component.Markup, StringComparison.Ordinal);
        Assert.Contains("one command per hour", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void AStateThatCannotBeLoadedIsReported()
    {
        StoredState(null, "Server unreachable");

        var component = RenderComponent();

        Assert.Contains("Could not load Tesla cloud state: Server unreachable", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderingTheSameCarAgainKeepsWhatTheUserDid()
    {
        //A guided flow re-renders this component every time it saves. Reloading then used to reset the screen.
        var component = RenderComponent();
        Button(component, AddKeyText).Find("a").Click();

        component.Render(parameters => parameters.Add(p => p.IsOptional, true));

        _homeService.Verify(s => s.GetFleetApiState(It.IsAny<int>()), Times.Once);
        Assert.Contains("You added the key but have not tested the connection yet.", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void AnotherCarLoadsItsOwnState()
    {
        var component = RenderComponent();

        component.Render(parameters => parameters.Add(p => p.CarId, 6));

        _homeService.Verify(s => s.GetFleetApiState(CarId), Times.Once);
        _homeService.Verify(s => s.GetFleetApiState(6), Times.Once);
    }

    [Fact]
    public void WithoutACarNothingIsShownOrAsked()
    {
        var component = RenderComponent(autoTest: true, carId: null);

        Assert.Empty(component.FindComponents<MudAlert>());
        Assert.Empty(component.FindComponents<MudButton>());
        _homeService.Verify(s => s.GetFleetApiState(It.IsAny<int>()), Times.Never);
        Assert.Equal(0, TestRequests());
    }
}
