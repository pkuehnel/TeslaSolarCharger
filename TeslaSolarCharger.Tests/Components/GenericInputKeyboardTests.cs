using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using MudExtensions.Services;
using TeslaSolarCharger.Client.Components;
using TeslaSolarCharger.Client.Components.StartPage;
using TeslaSolarCharger.Client.Helper;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Attributes;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Dtos.ChargingCost.CostConfigurations;
using Xunit;

namespace TeslaSolarCharger.Tests.Components;

/// <summary>
/// The numeric keypad iPhones show for numeric inputs has no minus key. Inputs whose value may be negative therefore
/// fall back to the text keyboard on iOS, every other numeric input keeps the keypad.
/// </summary>
public class GenericInputKeyboardTests : Bunit.TestContext
{
    private readonly Mock<IJavaScriptWrapper> _javaScriptWrapper = new();
    private readonly Model _model = new();
    private readonly List<string> _requestedUrls = new();

    private class Model
    {
        [AllowNegativeValues]
        public int? SignedInteger { get; set; }

        public int? UnsignedMeaningInteger { get; set; }

        [AllowNegativeValues]
        public decimal SignedDecimal { get; set; }

        public decimal UnsignedMeaningDecimal { get; set; }
    }

    public GenericInputKeyboardTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddMudExtensions();
        Services.AddSharedDependencies();
        Services.AddSingleton(_javaScriptWrapper.Object);
        Services.AddSingleton(new HttpClient(new PowerBufferHandler(_requestedUrls)) { BaseAddress = new Uri("http://localhost/"), });
    }

    /// <summary>Answers the home page's power buffer requests: changing it is allowed and it is currently -300 W.</summary>
    private sealed class PowerBufferHandler(List<string> requestedUrls) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            requestedUrls.Add(path);
            var json = path switch
            {
                "/api/BaseConfiguration/AllowPowerBufferChangeOnHome" => "{\"value\":true}",
                "/api/BaseConfiguration/PowerBuffer" => "{\"value\":-300}",
                _ => "{}",
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }

    private void OpenOnIos(bool isIos) => _javaScriptWrapper.Setup(j => j.IsIosDevice()).ReturnsAsync(isIos);

    private string? RenderedInputMode<TValue>(System.Linq.Expressions.Expression<Func<TValue?>> property)
    {
        var input = Render<GenericInput<TValue>>(parameters => parameters.Add(p => p.For, property));
        return input.Find("input").GetAttribute("inputmode");
    }

    [Fact]
    public void AnIntegerThatMayBeNegativeGetsTheTextKeyboardOnIos()
    {
        OpenOnIos(true);

        Assert.Equal("text", RenderedInputMode(() => _model.SignedInteger));
    }

    [Fact]
    public void AnIntegerThatMayBeNegativeKeepsTheKeypadAwayFromIos()
    {
        OpenOnIos(false);

        Assert.Equal("numeric", RenderedInputMode(() => _model.SignedInteger));
    }

    [Fact]
    public void AnIntegerThatMustNotBeNegativeKeepsTheKeypadOnIosWithoutAskingTheBrowser()
    {
        OpenOnIos(true);

        Assert.Equal("numeric", RenderedInputMode(() => _model.UnsignedMeaningInteger));
        _javaScriptWrapper.Verify(j => j.IsIosDevice(), Times.Never);
    }

    [Fact]
    public void ADecimalThatMayBeNegativeGetsTheTextKeyboardOnIos()
    {
        OpenOnIos(true);

        Assert.Equal("text", RenderedInputMode(() => _model.SignedDecimal));
    }

    [Fact]
    public void ADecimalThatMayBeNegativeKeepsTheDecimalKeypadAwayFromIos()
    {
        OpenOnIos(false);

        Assert.Equal("decimal", RenderedInputMode(() => _model.SignedDecimal));
    }

    [Fact]
    public void ADecimalThatMustNotBeNegativeKeepsTheDecimalKeypadOnIos()
    {
        OpenOnIos(true);

        Assert.Equal("decimal", RenderedInputMode(() => _model.UnsignedMeaningDecimal));
        _javaScriptWrapper.Verify(j => j.IsIosDevice(), Times.Never);
    }

    [Fact]
    public void TheBrowserIsAskedOnlyOnceEvenWhenTheInputRendersAgain()
    {
        OpenOnIos(true);
        var input = Render<GenericInput<int?>>(parameters => parameters.Add(p => p.For, () => _model.SignedInteger));

        input.Render(parameters => parameters.Add(p => p.LabelName, "Changed"));

        Assert.Equal("text", input.Find("input").GetAttribute("inputmode"));
        _javaScriptWrapper.Verify(j => j.IsIosDevice(), Times.Once);
    }

    [Fact]
    public void ANegativeValueTypedOnIosReachesTheModel()
    {
        OpenOnIos(true);
        var input = Render<GenericInput<int?>>(parameters => parameters.Add(p => p.For, () => _model.SignedInteger));

        input.Find("input").Change("-500");

        Assert.Equal(-500, _model.SignedInteger);
    }

    [Fact]
    public void ThePowerBufferOnTheHomePageCanBeSetNegativeOnIos()
    {
        OpenOnIos(true);
        var component = Render<PowerBufferComponent>();
        component.WaitForAssertion(() => Assert.Equal("text", component.Find("input").GetAttribute("inputmode")));

        component.Find("input").Change("-500");
        component.FindComponent<MudFab>().Find("button").Click();

        component.WaitForAssertion(() =>
            Assert.Contains("/api/BaseConfiguration/UpdatePowerBuffer?powerBuffer=-500", _requestedUrls));
    }

    [Theory]
    [InlineData(typeof(BaseConfigurationBase), nameof(BaseConfigurationBase.PowerBuffer))]
    [InlineData(typeof(BaseConfigurationBase), nameof(BaseConfigurationBase.DynamicMinSocCalculationBuffer))]
    [InlineData(typeof(BaseConfigurationBase), nameof(BaseConfigurationBase.HoldHomeBatteryChargeSocBuffer))]
    [InlineData(typeof(BaseConfigurationBase), nameof(BaseConfigurationBase.ChargeHomeBatterySocBuffer))]
    [InlineData(typeof(FixedPrice), nameof(FixedPrice.Value))]
    [InlineData(typeof(DtoChargePrice), nameof(DtoChargePrice.SpotPriceSurcharge))]
    public void SettingsThatMayBeNegativeAreMarkedAsSuch(Type type, string propertyName)
    {
        var property = type.GetProperty(propertyName)!;

        Assert.NotNull(property.GetCustomAttribute<AllowNegativeValuesAttribute>());
    }

    [Fact]
    public void AnIosBrowserIsRecognised()
    {
        JSInterop.Setup<string>("detectDevice").SetResult("iOS");

        Assert.True(new JavaScriptWrapper(JSInterop.JSRuntime, Mock.Of<ISnackbar>()).IsIosDevice().GetAwaiter().GetResult());
    }

    [Fact]
    public void AnyOtherBrowserIsNotTakenForIos()
    {
        JSInterop.Setup<string>("detectDevice").SetResult("Other");

        Assert.False(new JavaScriptWrapper(JSInterop.JSRuntime, Mock.Of<ISnackbar>()).IsIosDevice().GetAwaiter().GetResult());
    }

    [Fact]
    public void ABrowserThatCannotBeAskedIsNotTakenForIos()
    {
        JSInterop.Setup<string>("detectDevice").SetException(new JSException("detectDevice is not defined"));

        Assert.False(new JavaScriptWrapper(JSInterop.JSRuntime, Mock.Of<ISnackbar>()).IsIosDevice().GetAwaiter().GetResult());
    }
}
