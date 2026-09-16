using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using MudExtensions.Services;
using TeslaSolarCharger.Client.Components.BaseConfiguration.ValueSources;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Localization;
using TeslaSolarCharger.Shared.Localization.Contracts;
using TeslaSolarCharger.Shared.Localization.Registries;
using TeslaSolarCharger.Shared.Localization.Registries.Components;
using TeslaSolarCharger.SharedModel.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Components;

/// <summary>
/// The cards listing the value sources of one kind, each with what its results currently deliver.
/// </summary>
public class GenericValueConfigurationComponentTests : Bunit.TestContext
{
    private static readonly DateTimeOffset ReadAt = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    public GenericValueConfigurationComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddMudExtensions();
        Services.AddSharedDependencies();
        Services.AddSingleton(Mock.Of<IJavaScriptWrapper>());
    }

    private IRenderedComponent<GenericValueConfigurationComponent> RenderWith(params DtoOverviewValueResult[] results)
    {
        Render<MudPopoverProvider>();
        var overview = new DtoValueConfigurationOverview("Inverter") { Id = 1, };
        overview.Results.AddRange(results);
        return Render<GenericValueConfigurationComponent>(parameters => parameters
            .Add(c => c.SourceName, "REST")
            .Add(c => c.ConfigurationOverviews, new List<DtoValueConfigurationOverview> { overview, }));
    }

    private static List<string> ChipTexts(IRenderedComponent<GenericValueConfigurationComponent> component) =>
        component.FindComponents<MudChip<string>>().Select(c => c.Find(".mud-chip-content").TextContent.Trim()).ToList();

    [Fact]
    public void AResultThatDeliveredNothingIsShownAsNotAvailable()
    {
        var notAvailable = Services.GetRequiredService<ITextLocalizationService>()
            .Get<GenericValueConfigurationComponentLocalizationRegistry>(TranslationKeys.GenericValueConfigurationNotAvailable,
                typeof(SharedComponentLocalizationRegistry));

        var component = RenderWith(new DtoOverviewValueResult { Id = 10, UsedFor = ValueUsage.GridPower, });

        Assert.Equal(notAvailable, Assert.Single(ChipTexts(component)));
        //There is no refresh time to tell, so no "Last refreshed at" from the year 1 either.
        Assert.True(component.FindComponents<MudTooltip>().Single(t => t.Instance.RootClass == "w-100").Instance.Disabled);
    }

    [Fact]
    public void AResultWithAValueShowsItRoundedWithItsUnitAndRefreshTime()
    {
        var component = RenderWith(new DtoOverviewValueResult { Id = 10, UsedFor = ValueUsage.GridPower, Value = new(ReadAt, 1234.567m), });

        Assert.Equal($"{1234.57m.ToString(CultureInfo.CurrentCulture)} W", Assert.Single(ChipTexts(component)));
        var tooltip = component.FindComponents<MudTooltip>().Single(t => t.Instance.RootClass == "w-100").Instance;
        Assert.False(tooltip.Disabled);
        Assert.Equal($"Last refreshed at {ReadAt.LocalDateTime.ToLongTimeString()}", tooltip.Text);
    }

    [Fact]
    public void EveryResultIsShownOnItsOwn()
    {
        var component = RenderWith(
            new DtoOverviewValueResult { Id = 10, UsedFor = ValueUsage.HomeBatterySoc, Value = new(ReadAt, 55), },
            new DtoOverviewValueResult { Id = 11, UsedFor = ValueUsage.InverterPower, });

        var chipTexts = ChipTexts(component);
        Assert.Equal(2, chipTexts.Count);
        Assert.Equal($"{55m.ToString(CultureInfo.CurrentCulture)} %", chipTexts[0]);
        Assert.DoesNotContain("W", chipTexts[1]);
    }
}
