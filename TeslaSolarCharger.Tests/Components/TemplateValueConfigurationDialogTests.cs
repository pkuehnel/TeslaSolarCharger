using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Bunit;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using MudExtensions;
using MudExtensions.Services;
using Newtonsoft.Json.Linq;
using TeslaSolarCharger.Client.Components;
using TeslaSolarCharger.Client.Components.BaseConfiguration.ValueSources.Templates;
using TeslaSolarCharger.Client.Components.BaseConfiguration.ValueSources.Templates.ConfigurationEditForms;
using TeslaSolarCharger.Client.Components.Setup;
using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Sma;
using TeslaSolarCharger.Shared.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Components;

/// <summary>
/// The dialog a device is connected through. In setup the device has already been picked by name, so the dialog must
/// not ask what it is or what to call it; on the detailed page both stay, but the name no longer has to be invented.
/// </summary>
public class TemplateValueConfigurationDialogTests : Bunit.TestContext
{
    private readonly Mock<ITemplateValueConfigurationService> _service = new();
    private List<DtoValueConfigurationOverview> _existingDevices = new();
    private DtoTemplateValueConfigurationBase? _saved;

    public TemplateValueConfigurationDialogTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddMudExtensions();
        Services.AddSharedDependencies();
        //The same validators the app registers, so saving is refused here exactly when it would be refused there.
        Services.AddValidatorsFromAssemblyContaining<CarBasicConfigurationValidator>(ServiceLifetime.Singleton);
        Services.AddSingleton(Mock.Of<IHttpClientHelper>());

        _service.Setup(s => s.GetOverviews()).ReturnsAsync(() => _existingDevices);
        _service
            .Setup(s => s.SaveAsync(It.IsAny<DtoTemplateValueConfigurationBase>()))
            .Callback<DtoTemplateValueConfigurationBase>(c => _saved = c)
            .ReturnsAsync(new Result<int>(1, null, null));
        Services.AddSingleton(_service.Object);
    }

    [Fact]
    public void ADevicePickedInSetupIsNotAskedForAgain()
    {
        var provider = OpenFromSetup(TemplateValueGatherType.SmaHybridInverterModbus);

        Assert.Empty(provider.FindComponents<MudSelectExtended<TemplateValueGatherType?>>());
        Assert.False(AsksFor(provider, nameof(DtoTemplateValueConfigurationBase.Name)));
        //What is left is how to reach the device, and it is there straight away rather than after choosing a type.
        Assert.True(AsksFor(provider, nameof(DtoSmaInverterTemplateValueConfiguration.Host)));
        Assert.Contains("connection details", provider.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ADeviceConnectedInSetupIsNamedAfterItsMakeAndModel()
    {
        var provider = OpenFromSetup(TemplateValueGatherType.SmaHybridInverterModbus);

        EnterHostAndSave(provider);

        Assert.NotNull(_saved);
        Assert.Equal(TemplateValueGatherType.SmaHybridInverterModbus, _saved!.GatherType);
        Assert.Equal("SMA Hybrid Inverter Modbus", _saved.Name);
    }

    [Fact]
    public void ASecondIdenticalDeviceGetsANameThatTellsThemApart()
    {
        _existingDevices = new List<DtoValueConfigurationOverview> { new("SMA Hybrid Inverter Modbus") { Id = 3, }, };
        var provider = OpenFromSetup(TemplateValueGatherType.SmaHybridInverterModbus);

        EnterHostAndSave(provider);

        Assert.Equal("SMA Hybrid Inverter Modbus 2", _saved?.Name);
    }

    [Fact]
    public void ChangingADeviceInSetupKeepsTheNameItAlreadyHas()
    {
        _service
            .Setup(s => s.GetAsync(7))
            .ReturnsAsync(new Result<DtoTemplateValueConfigurationBase>(new DtoTemplateValueConfigurationBase
            {
                Id = 7,
                Name = "Garage inverter",
                GatherType = TemplateValueGatherType.SmaInverterModbus,
                Configuration = JObject.FromObject(new DtoSmaInverterTemplateValueConfiguration { Host = "10.0.0.2", }),
            }, null, null));

        var provider = Open(new DialogParameters<TemplateValueConfigurationDialog>
        {
            { x => x.ValueConfigurationId, 7 },
            { x => x.ShowNameAndType, false },
        });
        ClickSave(provider);

        Assert.Equal("Garage inverter", _saved?.Name);
        Assert.Equal(TemplateValueGatherType.SmaInverterModbus, _saved?.GatherType);
        //A name that is already there is not replaced, so there is nothing to look up.
        _service.Verify(s => s.GetOverviews(), Times.Never);
    }

    [Fact]
    public void TheDetailedPageStillAsksForTheNameAndType()
    {
        var provider = Open(new DialogParameters<TemplateValueConfigurationDialog>());

        Assert.Single(provider.FindComponents<MudSelectExtended<TemplateValueGatherType?>>());
        Assert.True(AsksFor(provider, nameof(DtoTemplateValueConfigurationBase.Name)));
    }

    [Fact]
    public void ChoosingATypeOnTheDetailedPageFillsInAnEmptyName()
    {
        var provider = Open(new DialogParameters<TemplateValueConfigurationDialog>());

        ChooseType(provider, TemplateValueGatherType.SmaInverterModbus);
        EnterHostAndSave(provider);

        Assert.Equal("SMA Inverter Modbus", _saved?.Name);
    }

    [Fact]
    public void AProposedNameFollowsTheTypeWhenTheTypeIsChangedAgain()
    {
        var provider = Open(new DialogParameters<TemplateValueConfigurationDialog>());

        ChooseType(provider, TemplateValueGatherType.SmaInverterModbus);
        ChooseType(provider, TemplateValueGatherType.SmaHybridInverterModbus);
        EnterHostAndSave(provider);

        Assert.Equal("SMA Hybrid Inverter Modbus", _saved?.Name);
    }

    [Fact]
    public void ANameTheUserTypedIsNeverReplaced()
    {
        var provider = Open(new DialogParameters<TemplateValueConfigurationDialog>());

        InputFor(provider, nameof(DtoTemplateValueConfigurationBase.Name)).Change("Roof");
        ChooseType(provider, TemplateValueGatherType.SmaInverterModbus);
        EnterHostAndSave(provider);

        Assert.Equal("Roof", _saved?.Name);
    }

    [Fact]
    public void ANameTypedAfterAProposalIsKeptWhenTheTypeChanges()
    {
        var provider = Open(new DialogParameters<TemplateValueConfigurationDialog>());

        ChooseType(provider, TemplateValueGatherType.SmaInverterModbus);
        InputFor(provider, nameof(DtoTemplateValueConfigurationBase.Name)).Change("Roof");
        ChooseType(provider, TemplateValueGatherType.SmaHybridInverterModbus);
        EnterHostAndSave(provider);

        Assert.Equal("Roof", _saved?.Name);
    }

    [Fact]
    public void PickingADeviceInSetupOpensItsConnectionFormRightAway()
    {
        //The device used to reach the dialog only as its title, so the form behind it stayed empty until the same
        //device was chosen a second time from a longer list.
        var provider = Render<MudDialogProvider>();
        var picker = Render<SetupEquipmentSourcePicker>(parameters => parameters
            .Add(p => p.Configurations, new List<DtoValueConfigurationOverview>()));
        var autocomplete = picker.FindComponent<MudAutocomplete<TemplateValueGatherType?>>();

        //Not awaited: picking waits for the dialog to be closed, and this test only looks at it while it is open.
        _ = picker.InvokeAsync(() => autocomplete.Instance.ValueChanged.InvokeAsync(TemplateValueGatherType.SmaHybridInverterModbus));

        provider.WaitForAssertion(() =>
        {
            Assert.Single(provider.FindComponents<SmaInverterEditForm>());
            Assert.Empty(provider.FindComponents<MudSelectExtended<TemplateValueGatherType?>>());
            Assert.False(AsksFor(provider, nameof(DtoTemplateValueConfigurationBase.Name)));
            Assert.Contains("SMA Hybrid Inverter Modbus", provider.Markup, StringComparison.Ordinal);
        });
    }

    private IRenderedComponent<MudDialogProvider> OpenFromSetup(TemplateValueGatherType type) =>
        Open(new DialogParameters<TemplateValueConfigurationDialog>
        {
            { x => x.PreselectedType, type },
            { x => x.ShowNameAndType, false },
        });

    private IRenderedComponent<MudDialogProvider> Open(DialogParameters<TemplateValueConfigurationDialog> parameters)
    {
        var provider = Render<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();
        provider.InvokeAsync(() => dialogService.ShowAsync<TemplateValueConfigurationDialog>("Device", parameters))
            .GetAwaiter().GetResult();
        provider.WaitForAssertion(() => Assert.Single(provider.FindComponents<TemplateValueConfigurationDialog>()));
        return provider;
    }

    private static void ChooseType(IRenderedComponent<MudDialogProvider> provider, TemplateValueGatherType type)
    {
        var select = provider.FindComponent<MudSelectExtended<TemplateValueGatherType?>>();
        provider.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(type)).GetAwaiter().GetResult();
    }

    private void EnterHostAndSave(IRenderedComponent<MudDialogProvider> provider)
    {
        InputFor(provider, nameof(DtoSmaInverterTemplateValueConfiguration.Host)).Change("192.168.1.50");
        ClickSave(provider);
    }

    private void ClickSave(IRenderedComponent<MudDialogProvider> provider)
    {
        provider.FindAll("button").Last(b => b.TextContent.Contains("Save", StringComparison.OrdinalIgnoreCase)).Click();
        provider.WaitForAssertion(() => Assert.NotNull(_saved));
    }

    private static bool AsksFor(IRenderedComponent<MudDialogProvider> provider, string propertyName) =>
        FindInputs(provider, propertyName).Any();

    private static AngleSharp.Dom.IElement InputFor(IRenderedComponent<MudDialogProvider> provider, string propertyName) =>
        Assert.Single(FindInputs(provider, propertyName)).Find("input");

    private static IEnumerable<IRenderedComponent<GenericInput<string>>> FindInputs(
        IRenderedComponent<MudDialogProvider> provider, string propertyName) =>
        provider.FindComponents<GenericInput<string>>()
            .Where(input => input.Instance.For?.Body is MemberExpression member && member.Member.Name == propertyName);
}
