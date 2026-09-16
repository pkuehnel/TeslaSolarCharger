using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using MudExtensions.Services;
using TeslaSolarCharger.Client.Components;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using Xunit;

namespace TeslaSolarCharger.Tests.Components;

/// <summary>
/// The overview on the car settings page of every way TeslaSolarCharger can reach a car, told in the same words the
/// setup assistant uses, and the page deciding whether it starts open.
/// </summary>
public class CarControlCapabilityComponentTests : Bunit.TestContext
{
    private readonly Mock<ICarSettingsService> _carSettingsService = new();
    private List<CarBasicConfiguration>? _cars = new();

    public CarControlCapabilityComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddMudExtensions();
        Services.AddSharedDependencies();
        Services.AddSingleton(Mock.Of<IJavaScriptWrapper>());

        _carSettingsService.Setup(s => s.GetCarBasicConfigurations()).ReturnsAsync(() => _cars);
        _carSettingsService.Setup(s => s.GetFleetApiLicenseInfo()).ReturnsAsync(new DtoCarLicenseInfo());
        _carSettingsService.Setup(s => s.GetFleetApiTokenState()).ReturnsAsync(TokenState.UpToDate);
        Services.AddSingleton(_carSettingsService.Object);
        Services.AddSingleton(Mock.Of<ICloudConnectionCheckService>());
        Services.AddSingleton(Mock.Of<IOAuthNotificationService>());
    }

    private IRenderedComponent<CarControlCapabilityComponent> RenderOverview(bool initiallyExpanded = true) =>
        Render<CarControlCapabilityComponent>(parameters => parameters
            .Add(c => c.InitiallyExpanded, initiallyExpanded));

    private static int CountOf(string text, string markup) =>
        markup.Split(text, StringSplitOptions.None).Length - 1;

    [Fact]
    public void TeslasAndOtherCarsAreOfferedTheirOwnRoutes()
    {
        var overview = RenderOverview();

        var teslaGroup = overview.Find(".car-routes-tesla").TextContent;
        var otherGroup = overview.Find(".car-routes-other").TextContent;

        Assert.Contains("For a Tesla", teslaGroup);
        Assert.Contains("Over Bluetooth, from a device near the car", teslaGroup);
        Assert.Contains("Over the internet, through your Tesla account", teslaGroup);
        Assert.DoesNotContain("Charging station only", teslaGroup);

        Assert.Contains("For any other car", otherGroup);
        Assert.Contains("Charging station, with battery level from your car account", otherGroup);
        Assert.Contains("Charging station only", otherGroup);
        Assert.DoesNotContain("through your Tesla account", otherGroup);
    }

    [Fact]
    public void EveryRouteSaysWhatControlsChargingWhereTheBatteryLevelComesFromAndWhatItCosts()
    {
        var markup = RenderOverview().Markup;

        //The same three lines as in the setup assistant, once for each of the four routes.
        Assert.Equal(4, CountOf("Charging is controlled through:", markup));
        Assert.Equal(4, CountOf("Battery level comes from:", markup));
        Assert.Equal(4, CountOf("Extra subscription for this car:", markup));
        Assert.Equal(2, CountOf("none beyond the base licence", markup));
        Assert.Equal(2, CountOf("yes, one subscription for this car", markup));
        Assert.Equal(4, CountOf("Needs:", markup));
    }

    [Fact]
    public void ACarWithoutADataConnectionNamesBothWaysToKnowItsBatteryLevel()
    {
        //Nobody has been asked here, so neither the estimate nor typing it in is presented as the only way.
        var otherGroup = RenderOverview().Find(".car-routes-other").TextContent;

        Assert.Contains("an estimate from how much was charged, or what you enter yourself", otherGroup);
    }

    [Fact]
    public void OnlyThePaidRoutesLinkToTheSubscriptions()
    {
        var overview = RenderOverview();
        var subscriptionsUrl = Services.GetRequiredService<IConstants>().SubscriptionsUrl;

        IEnumerable<IElement> LinksIn(string group) => overview.Find(group).QuerySelectorAll("a")
            .Where(a => a.GetAttribute("href") == subscriptionsUrl);

        //One per group: the internet route for a Tesla, the car account route for any other car.
        var teslaLink = Assert.Single(LinksIn(".car-routes-tesla"));
        var otherLink = Assert.Single(LinksIn(".car-routes-other"));
        Assert.Equal("_blank", teslaLink.GetAttribute("target"));
        Assert.Contains("See subscriptions and prices", teslaLink.TextContent);
        Assert.Contains("See subscriptions and prices", otherLink.TextContent);
    }

    [Fact]
    public void NoPriceIsWrittenIntoTheOverview()
    {
        //Prices change on the subscriptions page; a number copied here would silently go stale.
        var markup = RenderOverview().Markup;

        Assert.DoesNotContain("€", markup);
        Assert.DoesNotContain("month", markup, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheOverviewStartsTheWayThePageAsks(bool initiallyExpanded)
    {
        var overview = RenderOverview(initiallyExpanded);

        Assert.Equal(initiallyExpanded, overview.FindComponent<MudExpansionPanel>().Instance.Expanded);
    }

    [Fact]
    public void AClosedOverviewCanBeOpened()
    {
        var overview = RenderOverview(initiallyExpanded: false);

        overview.Find(".mud-expand-panel-header").Click();

        Assert.True(overview.FindComponent<MudExpansionPanel>().Instance.Expanded);
    }

    [Fact]
    public void ClosingTheOverviewIsNotUndoneWhenThePageRendersAgain()
    {
        var overview = RenderOverview(initiallyExpanded: true);
        overview.Find(".mud-expand-panel-header").Click();
        Assert.False(overview.FindComponent<MudExpansionPanel>().Instance.Expanded);

        overview.Render(parameters => parameters.Add(c => c.InitiallyExpanded, true));

        Assert.False(overview.FindComponent<MudExpansionPanel>().Instance.Expanded);
    }

    [Fact]
    public void TheCarSettingsPageOpensTheOverviewWhileThereIsNoCarYet()
    {
        _cars = new List<CarBasicConfiguration>();

        var page = Render<CarManagementComponent>();

        page.WaitForAssertion(() => Assert.True(page.FindComponent<MudExpansionPanel>().Instance.Expanded));
    }

    [Fact]
    public void TheCarSettingsPageShowsExistingCarsFirstAndKeepsTheOverviewClosed()
    {
        _cars = new List<CarBasicConfiguration>
        {
            new() { Id = 1, Name = "Our car", Vin = "VIN1", CarType = CarType.Tesla, },
        };

        var page = Render<CarManagementComponent>();

        page.WaitForAssertion(() => Assert.False(page.FindComponent<MudExpansionPanel>().Instance.Expanded));
    }

    [Fact]
    public void TheCarSettingsPageDoesNotGuessBeforeItKnowsTheCars()
    {
        //Still loading: opening or closing now would be decided on a car list nobody has seen yet.
        var pending = new TaskCompletionSource<List<CarBasicConfiguration>?>();
        _carSettingsService.Setup(s => s.GetCarBasicConfigurations()).Returns(pending.Task);

        var page = Render<CarManagementComponent>();

        Assert.Empty(page.FindComponents<CarControlCapabilityComponent>());

        pending.SetResult(new List<CarBasicConfiguration>());
        page.WaitForAssertion(() => Assert.True(page.FindComponent<MudExpansionPanel>().Instance.Expanded));
    }

    [Fact]
    public void TheCarSettingsPageCanLeaveTheOverviewOut()
    {
        var page = Render<CarManagementComponent>(parameters => parameters.Add(c => c.ShowControlCapabilityInfo, false));

        //Waiting for the car list first, so the overview is missing because it was left out and not because the
        //page is still loading.
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindComponents<RightAlignedButtonComponent>()));
        Assert.Empty(page.FindComponents<CarControlCapabilityComponent>());
    }
}
