using System.Globalization;
using Bunit;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using MudExtensions.Services;
using TeslaSolarCharger.Client.Components.BaseConfiguration.ValueSources.Templates.ConfigurationEditForms;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Sma;
using TeslaSolarCharger.Shared.Localization;
using TeslaSolarCharger.Shared.Localization.Contracts;
using TeslaSolarCharger.Shared.Localization.Registries;
using Xunit;

namespace TeslaSolarCharger.Tests.Components;

/// <summary>
/// The Sunny Portal's forecast-based battery charging overrides the battery control of Solar4Car about once a minute,
/// so whoever lets Solar4Car control an SMA hybrid inverter's battery is told to disable it.
/// </summary>
public class SmaInverterEditFormTests : Bunit.TestContext
{
    public SmaInverterEditFormTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddMudExtensions();
        Services.AddSharedDependencies();
        Services.AddValidatorsFromAssemblyContaining<CarBasicConfigurationValidator>(ServiceLifetime.Singleton);
        Services.AddSingleton(Mock.Of<IHttpClientHelper>());
        Services.AddSingleton(Mock.Of<IJavaScriptWrapper>());
    }

    [Fact]
    public void EnabledBatteryControlShowsTheForecastBasedChargingHint()
    {
        var form = RenderForm(showHomeBatteryControlFields: true, enableHomeBatteryControl: true);

        Assert.Contains(Hint(), form.Markup);
        Assert.Single(form.FindComponents<MudAlert>());
    }

    [Fact]
    public void DisabledBatteryControlShowsNoHint()
    {
        var form = RenderForm(showHomeBatteryControlFields: true, enableHomeBatteryControl: false);

        Assert.DoesNotContain(Hint(), form.Markup);
        Assert.Empty(form.FindComponents<MudAlert>());
    }

    [Fact]
    public void AnInverterWithoutBatteryControlShowsNoHintEvenIfTheStoredConfigurationEnablesIt()
    {
        var form = RenderForm(showHomeBatteryControlFields: false, enableHomeBatteryControl: true);

        Assert.DoesNotContain(Hint(), form.Markup);
        Assert.Empty(form.FindComponents<MudAlert>());
    }

    [Fact]
    public void TheHintIsTranslatedIntoEveryLanguage()
    {
        var registry = new SharedComponentLocalizationRegistry();

        var english = registry.Get(TranslationKeys.SmaInverterEditFormForecastBasedChargingHint, new CultureInfo("en"));
        var german = registry.Get(TranslationKeys.SmaInverterEditFormForecastBasedChargingHint, new CultureInfo("de"));

        //The names SMA uses in the Sunny Portal, so users find the setting.
        Assert.Contains("Forecast-based battery charging", english);
        Assert.Contains("Prognosebasierte Batterieladen", german);
    }

    private IRenderedComponent<SmaInverterEditForm> RenderForm(bool showHomeBatteryControlFields, bool enableHomeBatteryControl) =>
        Render<SmaInverterEditForm>(parameters => parameters
            .Add(p => p.Configuration, new DtoSmaInverterTemplateValueConfiguration
            {
                Host = "10.0.0.2",
                EnableHomeBatteryControl = enableHomeBatteryControl,
                MaxBatteryChargePowerW = 5000,
                MaxBatteryDischargePowerW = 5000,
            })
            .Add(p => p.ShowHomeBatteryControlFields, showHomeBatteryControlFields));

    private string Hint() =>
        Services.GetRequiredService<ITextLocalizationService>()
            .Get<SharedComponentLocalizationRegistry>(TranslationKeys.SmaInverterEditFormForecastBasedChargingHint)!;
}
