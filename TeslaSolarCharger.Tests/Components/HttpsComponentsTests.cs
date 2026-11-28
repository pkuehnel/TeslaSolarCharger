using System.Globalization;
using System.Linq;
using System.Threading;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using MudExtensions.Services;
using TeslaSolarCharger.Client.Components.BaseConfiguration;
using TeslaSolarCharger.Client.Components.Https;
using TeslaSolarCharger.Client.Components.StartPage;
using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Client.Helper;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Dtos.Https;
using TeslaSolarCharger.Shared.Localization;
using TeslaSolarCharger.Shared.Localization.Contracts;
using TeslaSolarCharger.Shared.Localization.Registries;
using TeslaSolarCharger.Shared.Localization.Registries.Components;
using Xunit;

namespace TeslaSolarCharger.Tests.Components;

/// <summary>
/// The HTTPS hint on the home page and the HTTPS section of the base configuration.
/// </summary>
public class HttpsComponentsTests : Bunit.TestContext
{
    private readonly Mock<IHttpClientHelper> _httpClientHelper = new();
    private readonly Mock<IJavaScriptWrapper> _javaScriptWrapper = new();

    private static readonly DtoHttpsInformation Enabled = new()
    {
        IsEnabled = true,
        Reachability = HttpsReachability.Reachable,
        Port = 7443,
        CoveredNames = ["127.0.0.1", "localhost", "primary-pc",],
        RootCertificateName = "Solar4Car local HTTPS root 2026-09-22 12:00",
        RootCertificateSha256Fingerprint = "AB:CD",
        RootCertificateValidUntil = new(2036, 9, 20, 12, 0, 0, System.TimeSpan.Zero),
    };

    private static readonly DtoHttpsInformation Forwarded = WithReachability(HttpsReachability.Forwarded);

    private static DtoHttpsInformation WithReachability(HttpsReachability reachability) => new()
    {
        IsEnabled = Enabled.IsEnabled,
        Reachability = reachability,
        Port = Enabled.Port,
        CoveredNames = Enabled.CoveredNames,
        RootCertificateName = Enabled.RootCertificateName,
        RootCertificateSha256Fingerprint = Enabled.RootCertificateSha256Fingerprint,
        RootCertificateValidUntil = Enabled.RootCertificateValidUntil,
    };

    public HttpsComponentsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddMudExtensions();
        Services.AddSharedDependencies();
        Services.AddSingleton(_httpClientHelper.Object);
        //A browser that is slowed down without HTTPS and has not dismissed the hint yet
        _javaScriptWrapper.Setup(j => j.IsWebKitBrowser()).ReturnsAsync(true);
        _javaScriptWrapper.Setup(j => j.ReadFromLocalStorage(It.IsAny<string>())).ReturnsAsync((string?)null);
        Services.AddSingleton(_javaScriptWrapper.Object);
    }

    private void AnswerHttpsInformation(DtoHttpsInformation? information, string? errorMessage = null)
    {
        _httpClientHelper
            .Setup(h => h.SendGetRequestAsync<DtoHttpsInformation>(HttpsUrlHelper.HttpsInformationPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<DtoHttpsInformation>(information, errorMessage, null));
        _httpClientHelper
            .Setup(h => h.SendGetRequestWithSnackbarAsync<DtoHttpsInformation>(HttpsUrlHelper.HttpsInformationPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(information);
    }

    private void OpenPageSecurely() => Services.AddSingleton<NavigationManager>(new SecureNavigationManager());

    private string T(string key) =>
        Services.GetRequiredService<ITextLocalizationService>()
            .Get<HttpsComponentsLocalizationRegistry>(key, typeof(SharedComponentLocalizationRegistry))!;

    private string PortPublishingHint => string.Format(CultureInfo.CurrentCulture, T(TranslationKeys.HttpsPortPublishingUnverifiedHint), 7443);

    private string EnabledStatus => string.Format(CultureInfo.CurrentCulture, T(TranslationKeys.HttpsEnabledStatus), 7443);

    [Fact]
    public void TheHintOffersTheCertificateAndTheSecureAddressOverHttp()
    {
        AnswerHttpsInformation(Enabled);

        var hint = Render<HttpsHintComponent>();

        Assert.Contains(T(TranslationKeys.HttpsHintTitle), hint.Markup);
        Assert.Contains(T(TranslationKeys.HttpsDownloadCertificateButton), hint.Markup);
        var secureAddress = hint.FindComponents<MudButton>().Single(b => b.Instance.Href != default);
        Assert.Equal("https://localhost:7443/", secureAddress.Instance.Href);
    }

    [Fact]
    public void TheHintPointsToTheSamePageOnTheHttpsPort()
    {
        AnswerHttpsInformation(Enabled);
        Services.GetRequiredService<NavigationManager>().NavigateTo("Home?tab=2");

        var hint = Render<HttpsHintComponent>();

        Assert.Equal("https://localhost:7443/Home?tab=2", hint.FindComponents<MudButton>().Single(b => b.Instance.Href != default).Instance.Href);
    }

    [Fact]
    public void TheDownloadButtonLoadsTheCertificateAsWholePage()
    {
        AnswerHttpsInformation(Enabled);
        var hint = Render<HttpsHintComponent>();

        hint.FindComponents<MudButton>().Single(b => b.Instance.Href == default).Find("button").Click();

        var navigation = Services.GetRequiredService<BunitNavigationManager>().History.First();
        Assert.Equal(HttpsUrlHelper.RootCertificatePath, navigation.Uri);
        Assert.True(navigation.Options.ForceLoad);
    }

    [Fact]
    public void TheHintIsHiddenWhenHttpsIsNotAvailable()
    {
        AnswerHttpsInformation(new DtoHttpsInformation { IsEnabled = false, });

        Assert.Empty(Render<HttpsHintComponent>().Markup.Trim());
    }

    [Fact]
    public void TheHintIsHiddenWhenTheServerCannotBeAsked()
    {
        AnswerHttpsInformation(null, "Not reachable");

        Assert.Empty(Render<HttpsHintComponent>().Markup.Trim());
    }

    [Fact]
    public void TheHintIsHiddenOverHttpsWithoutAskingTheServer()
    {
        AnswerHttpsInformation(Enabled);
        OpenPageSecurely();

        Assert.Empty(Render<HttpsHintComponent>().Markup.Trim());
        _httpClientHelper.Verify(h => h.SendGetRequestAsync<DtoHttpsInformation>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void TheHintIsHiddenOnBrowsersThatAreNotSlowedDown()
    {
        AnswerHttpsInformation(Enabled);
        _javaScriptWrapper.Setup(j => j.IsWebKitBrowser()).ReturnsAsync(false);

        Assert.Empty(Render<HttpsHintComponent>().Markup.Trim());
        _httpClientHelper.Verify(h => h.SendGetRequestAsync<DtoHttpsInformation>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void TheHintIsHiddenAfterItWasDismissedOnThisDevice()
    {
        AnswerHttpsInformation(Enabled);
        _javaScriptWrapper.Setup(j => j.ReadFromLocalStorage(HttpsHintComponent.DismissedStorageKey)).ReturnsAsync("true");

        Assert.Empty(Render<HttpsHintComponent>().Markup.Trim());
        _httpClientHelper.Verify(h => h.SendGetRequestAsync<DtoHttpsInformation>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ClosingTheHintHidesItAndRemembersThat()
    {
        AnswerHttpsInformation(Enabled);
        var hint = Render<HttpsHintComponent>();

        hint.FindComponents<MudIconButton>().Single().Find("button").Click();

        Assert.Empty(hint.Markup.Trim());
        _javaScriptWrapper.Verify(j => j.SaveToLocalStorage(HttpsHintComponent.DismissedStorageKey, "true"), Times.Once);
    }

    [Fact]
    public void TheSectionIgnoresTheBrowserAndTheDismissal()
    {
        //The base configuration is where users look for the certificate on purpose
        AnswerHttpsInformation(Enabled);
        _javaScriptWrapper.Setup(j => j.IsWebKitBrowser()).ReturnsAsync(false);
        _javaScriptWrapper.Setup(j => j.ReadFromLocalStorage(HttpsHintComponent.DismissedStorageKey)).ReturnsAsync("true");

        Assert.Contains(T(TranslationKeys.HttpsDownloadCertificateButton), Render<HttpsConfigurationComponent>().Markup);
    }

    [Fact]
    public void TheHintOffersTheSecureAddressAndExplainsHowToPublishThePortBehindAPortMapping()
    {
        //A Docker bridge network: whether the HTTPS port is published cannot be seen from inside the container
        AnswerHttpsInformation(Forwarded);

        var hint = Render<HttpsHintComponent>();

        Assert.Contains(T(TranslationKeys.HttpsHintText), hint.Markup);
        Assert.Contains(PortPublishingHint, hint.Markup);
        Assert.Contains(T(TranslationKeys.HttpsDownloadCertificateButton), hint.Markup);
        Assert.Contains(T(TranslationKeys.HttpsInstallInstructionsTitle), hint.Markup);
        Assert.Equal("https://localhost:7443/", hint.FindComponents<MudButton>().Single(b => b.Instance.Href != default).Instance.Href);
    }

    [Theory]
    [InlineData(HttpsReachability.Reachable)]
    [InlineData(HttpsReachability.Unknown)]
    public void TheHintDoesNotMentionPublishingThePortWhenTheBrowserTalksToSolar4CarDirectly(HttpsReachability reachability)
    {
        AnswerHttpsInformation(WithReachability(reachability));

        var hint = Render<HttpsHintComponent>();

        Assert.DoesNotContain(PortPublishingHint, hint.Markup);
        Assert.Contains(hint.FindComponents<MudButton>(), b => b.Instance.Href != default);
    }

    [Fact]
    public void TheSectionShowsANeutralStatusAndTheSecureAddressBehindAPortMapping()
    {
        //E.g. a Docker bridge network that publishes 7190 and 7443: it must not be reported as unpublished
        AnswerHttpsInformation(Forwarded);

        var section = Render<HttpsConfigurationComponent>();

        Assert.Equal([Severity.Info,], section.FindComponents<MudAlert>().Select(a => a.Instance.Severity));
        Assert.Contains(PortPublishingHint, section.Markup);
        Assert.DoesNotContain(EnabledStatus, section.Markup);
        Assert.Contains(T(TranslationKeys.HttpsDownloadCertificateButton), section.Markup);
        Assert.Equal("https://localhost:7443/", section.FindComponents<MudButton>().Single(b => b.Instance.Href != default).Instance.Href);
    }

    [Fact]
    public void TheSectionShowsHttpsAsAvailableBehindAPortMappingWhenOpenOverHttps()
    {
        //The page itself came through the HTTPS port, e.g. a Docker bridge network that publishes only 7443
        AnswerHttpsInformation(Forwarded);
        OpenPageSecurely();

        var section = Render<HttpsConfigurationComponent>();

        Assert.Equal([Severity.Success,], section.FindComponents<MudAlert>().Select(a => a.Instance.Severity));
        Assert.Contains(EnabledStatus, section.Markup);
        Assert.DoesNotContain(PortPublishingHint, section.Markup);
        Assert.DoesNotContain(section.FindComponents<MudButton>(), b => b.Instance.Href != default);
    }

    [Theory]
    [InlineData(HttpsReachability.Reachable)]
    [InlineData(HttpsReachability.Unknown)]
    public void TheSectionShowsHttpsAsAvailableWhenTheBrowserTalksToSolar4CarDirectly(HttpsReachability reachability)
    {
        //E.g. network_mode: host or no Docker at all
        AnswerHttpsInformation(WithReachability(reachability));

        var section = Render<HttpsConfigurationComponent>();

        Assert.Equal([Severity.Success,], section.FindComponents<MudAlert>().Select(a => a.Instance.Severity));
        Assert.Contains(EnabledStatus, section.Markup);
        Assert.DoesNotContain(PortPublishingHint, section.Markup);
        Assert.Contains(section.FindComponents<MudButton>(), b => b.Instance.Href != default);
    }

    [Fact]
    public void TheSectionShowsThePortTheNamesAndTheRootCertificate()
    {
        AnswerHttpsInformation(Enabled);

        var section = Render<HttpsConfigurationComponent>();

        Assert.Contains(string.Format(CultureInfo.CurrentCulture, T(TranslationKeys.HttpsEnabledStatus), 7443), section.Markup);
        Assert.Contains("127.0.0.1, localhost, primary-pc", section.Markup);
        Assert.Contains(Enabled.RootCertificateName!, section.Markup);
        Assert.Contains("AB:CD", section.Markup);
        Assert.Contains(T(TranslationKeys.HttpsDownloadCertificateButton), section.Markup);
    }

    [Fact]
    public void TheSectionWarnsWhenHttpsIsNotAvailable()
    {
        //The reachability is reported even without HTTPS, e.g. HttpsPort=0 in a Docker bridge network
        AnswerHttpsInformation(new DtoHttpsInformation { IsEnabled = false, Reachability = HttpsReachability.Forwarded, });

        var section = Render<HttpsConfigurationComponent>();

        Assert.Contains(T(TranslationKeys.HttpsDisabledStatus), section.Markup);
        Assert.DoesNotContain(PortPublishingHint, section.Markup);
        //Without HTTPS there is no secure address to open, but the certificate can still be installed
        Assert.DoesNotContain(section.FindComponents<MudButton>(), b => b.Instance.Href != default);
        Assert.Contains(T(TranslationKeys.HttpsDownloadCertificateButton), section.Markup);
    }

    [Fact]
    public void TheSectionOffersNoSecureAddressWithoutAKnownHttpsPort()
    {
        AnswerHttpsInformation(new DtoHttpsInformation { IsEnabled = true, Port = null, Reachability = HttpsReachability.Forwarded, });

        var section = Render<HttpsConfigurationComponent>();

        Assert.DoesNotContain(section.FindComponents<MudButton>(), b => b.Instance.Href != default);
        Assert.Contains(T(TranslationKeys.HttpsDownloadCertificateButton), section.Markup);
    }

    [Fact]
    public void TheSectionOffersNoSecureAddressWhenAlreadyOnHttps()
    {
        AnswerHttpsInformation(Enabled);
        OpenPageSecurely();

        var section = Render<HttpsConfigurationComponent>();

        Assert.DoesNotContain(section.FindComponents<MudButton>(), b => b.Instance.Href != default);
    }

    [Fact]
    public void TheInstructionsListEachStepOfEachDevice()
    {
        var instructions = Render<HttpsInstallInstructionsComponent>();
        instructions.Find(".mud-expand-panel-header").Click();

        var expectedSteps = new[]
            {
                TranslationKeys.HttpsInstallIosSteps, TranslationKeys.HttpsInstallAndroidSteps,
                TranslationKeys.HttpsInstallWindowsSteps, TranslationKeys.HttpsInstallMacSteps,
            }
            .SelectMany(key => T(key).Split('\n'))
            .ToList();
        Assert.Equal(expectedSteps, instructions.FindAll("li").Select(li => li.TextContent.Trim()).ToList());
        Assert.Contains(T(TranslationKeys.HttpsInstallIosTitle), instructions.Markup);
    }

    private sealed class SecureNavigationManager : NavigationManager
    {
        public SecureNavigationManager() => Initialize("https://localhost/", "https://localhost/");
    }
}
