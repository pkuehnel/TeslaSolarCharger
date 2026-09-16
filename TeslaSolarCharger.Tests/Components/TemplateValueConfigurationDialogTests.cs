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
using TeslaSolarCharger.Shared.Helper;
using Xunit;

namespace TeslaSolarCharger.Tests.Components;

/// <summary>
/// The dialog a device is connected through. A device is chosen by its brand first and then from that brand's devices.
/// In setup the dialog never asks what to call the device, and once a device is added only how to reach it is left to
/// change; on the detailed page the name stays, but no longer has to be invented.
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
        _service
            .Setup(s => s.GetAsync(7))
            .ReturnsAsync(new Result<DtoTemplateValueConfigurationBase>(new DtoTemplateValueConfigurationBase
            {
                Id = 7,
                Name = "Garage inverter",
                GatherType = TemplateValueGatherType.SmaInverterModbus,
                Configuration = JObject.FromObject(new DtoSmaInverterTemplateValueConfiguration { Host = "10.0.0.2", }),
            }, null, null));
        Services.AddSingleton(_service.Object);
    }

    [Fact]
    public void ADeviceAddedInSetupIsChosenByBrandAndThenByDeviceWithoutAName()
    {
        var provider = AddFromSetup();

        Assert.Single(provider.FindComponents<MudSelect<string>>());
        Assert.Single(provider.FindComponents<MudSelect<TemplateValueGatherType?>>());
        Assert.False(AsksFor(provider, nameof(DtoTemplateValueConfigurationBase.Name)));
    }

    [Fact]
    public void TheDeviceListWaitsForABrand()
    {
        var provider = AddFromSetup();

        Assert.True(DeviceSelect(provider).Instance.Disabled);
        Assert.Empty(provider.FindComponents<SmaInverterEditForm>());
    }

    [Fact]
    public void ChoosingABrandOffersOnlyThatBrandsDevices()
    {
        var popovers = Render<MudPopoverProvider>();
        var provider = AddFromSetup();

        ChooseBrand(provider, "SMA");
        var deviceSelect = DeviceSelect(provider);
        provider.InvokeAsync(() => deviceSelect.Instance.OpenMenu()).GetAwaiter().GetResult();

        popovers.WaitForAssertion(() =>
        {
            var offered = popovers.FindComponents<MudSelectItem<TemplateValueGatherType?>>()
                .Select(item => item.Instance.Value)
                .ToList();
            Assert.False(deviceSelect.Instance.Disabled);
            Assert.Equal(TemplateValueGatherTypeVendors.GetTypesOf("SMA").Select(t => (TemplateValueGatherType?)t), offered);
        });
        //Offered under the brand, so not repeated in front of every device.
        Assert.Contains("Hybrid Inverter Modbus", popovers.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Sma Hybrid Inverter Modbus", popovers.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ABrandWithASingleDeviceTakesItStraightAway()
    {
        var provider = AddFromSetup();

        ChooseBrand(provider, "Tesla");

        Assert.Equal(TemplateValueGatherType.TeslaPowerwallFleetApi, DeviceSelect(provider).Instance.Value);
        Assert.Single(provider.FindComponents<TeslaPowerwallEditForm>());
    }

    [Fact]
    public void ABrandWithSeveralDevicesLeavesTheChoiceToTheUser()
    {
        var provider = AddFromSetup();

        ChooseBrand(provider, "SMA");

        Assert.Null(DeviceSelect(provider).Instance.Value);
        Assert.Empty(provider.FindComponents<SmaInverterEditForm>());
    }

    [Fact]
    public void ChoosingTheDeviceShowsHowToReachIt()
    {
        var provider = AddFromSetup();

        ChooseDevice(provider, TemplateValueGatherType.SmaHybridInverterModbus);

        Assert.Single(provider.FindComponents<SmaInverterEditForm>());
        Assert.True(AsksFor(provider, nameof(DtoSmaInverterTemplateValueConfiguration.Host)));
    }

    [Fact]
    public void ChoosingAnotherBrandDropsTheDeviceOfThePreviousOne()
    {
        var provider = AddFromSetup();
        ChooseDevice(provider, TemplateValueGatherType.SmaHybridInverterModbus);

        ChooseBrand(provider, "Fronius");

        Assert.Null(DeviceSelect(provider).Instance.Value);
        Assert.Empty(provider.FindComponents<SmaInverterEditForm>());
        Assert.True(SaveButton(provider).HasAttribute("disabled"));
    }

    [Fact]
    public void ChoosingTheSameBrandAgainKeepsTheChosenDevice()
    {
        var provider = AddFromSetup();
        ChooseDevice(provider, TemplateValueGatherType.SmaHybridInverterModbus);

        ChooseBrand(provider, "SMA");

        Assert.Equal(TemplateValueGatherType.SmaHybridInverterModbus, DeviceSelect(provider).Instance.Value);
        Assert.Single(provider.FindComponents<SmaInverterEditForm>());
    }

    [Fact]
    public void SavingIsOnlyOfferedOnceADeviceIsChosen()
    {
        var provider = AddFromSetup();
        Assert.True(SaveButton(provider).HasAttribute("disabled"));

        ChooseDevice(provider, TemplateValueGatherType.SmaHybridInverterModbus);

        Assert.False(SaveButton(provider).HasAttribute("disabled"));
    }

    [Fact]
    public void ADeviceConnectedInSetupIsNamedAfterItsMakeAndModel()
    {
        var provider = AddFromSetup();

        ChooseDevice(provider, TemplateValueGatherType.SmaHybridInverterModbus);
        EnterHostAndSave(provider);

        Assert.NotNull(_saved);
        Assert.Equal(TemplateValueGatherType.SmaHybridInverterModbus, _saved!.GatherType);
        Assert.Equal("SMA Hybrid Inverter Modbus", _saved.Name);
    }

    [Fact]
    public void ASecondIdenticalDeviceGetsANameThatTellsThemApart()
    {
        _existingDevices = new List<DtoValueConfigurationOverview> { new("SMA Hybrid Inverter Modbus") { Id = 3, }, };
        var provider = AddFromSetup();

        ChooseDevice(provider, TemplateValueGatherType.SmaHybridInverterModbus);
        EnterHostAndSave(provider);

        Assert.Equal("SMA Hybrid Inverter Modbus 2", _saved?.Name);
    }

    [Fact]
    public void ChangingADeviceInSetupOnlyAsksHowToReachIt()
    {
        var provider = Open(new DialogParameters<TemplateValueConfigurationDialog>
        {
            { x => x.ValueConfigurationId, 7 },
            { x => x.ShowName, false },
            { x => x.ShowTypeSelection, false },
        });

        Assert.Empty(provider.FindComponents<MudSelect<string>>());
        Assert.Empty(provider.FindComponents<MudSelect<TemplateValueGatherType?>>());
        Assert.False(AsksFor(provider, nameof(DtoTemplateValueConfigurationBase.Name)));
        Assert.True(AsksFor(provider, nameof(DtoSmaInverterTemplateValueConfiguration.Host)));
        Assert.Contains("connection details", provider.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChangingADeviceInSetupKeepsTheNameItAlreadyHas()
    {
        var provider = Open(new DialogParameters<TemplateValueConfigurationDialog>
        {
            { x => x.ValueConfigurationId, 7 },
            { x => x.ShowName, false },
            { x => x.ShowTypeSelection, false },
        });
        ClickSave(provider);

        Assert.Equal("Garage inverter", _saved?.Name);
        Assert.Equal(TemplateValueGatherType.SmaInverterModbus, _saved?.GatherType);
        //A name that is already there is not replaced, so there is nothing to look up.
        _service.Verify(s => s.GetOverviews(), Times.Never);
    }

    [Fact]
    public void TheDetailedPageAsksForTheNameTheBrandAndTheDevice()
    {
        var provider = Open(new DialogParameters<TemplateValueConfigurationDialog>());

        Assert.True(AsksFor(provider, nameof(DtoTemplateValueConfigurationBase.Name)));
        Assert.Single(provider.FindComponents<MudSelect<string>>());
        Assert.Single(provider.FindComponents<MudSelect<TemplateValueGatherType?>>());
    }

    [Fact]
    public void OpeningAConnectedDeviceShowsItsBrandAndDevice()
    {
        var provider = Open(new DialogParameters<TemplateValueConfigurationDialog> { { x => x.ValueConfigurationId, 7 }, });

        Assert.Equal("SMA", BrandSelect(provider).Instance.Value);
        Assert.Equal(TemplateValueGatherType.SmaInverterModbus, DeviceSelect(provider).Instance.Value);
        Assert.False(DeviceSelect(provider).Instance.Disabled);
    }

    [Fact]
    public void ChoosingADeviceOnTheDetailedPageFillsInAnEmptyName()
    {
        var provider = Open(new DialogParameters<TemplateValueConfigurationDialog>());

        ChooseDevice(provider, TemplateValueGatherType.SmaInverterModbus);
        EnterHostAndSave(provider);

        Assert.Equal("SMA Inverter Modbus", _saved?.Name);
    }

    [Fact]
    public void AProposedNameFollowsTheDeviceWhenTheDeviceIsChangedAgain()
    {
        var provider = Open(new DialogParameters<TemplateValueConfigurationDialog>());

        ChooseDevice(provider, TemplateValueGatherType.SmaInverterModbus);
        ChooseDevice(provider, TemplateValueGatherType.SmaHybridInverterModbus);
        EnterHostAndSave(provider);

        Assert.Equal("SMA Hybrid Inverter Modbus", _saved?.Name);
    }

    [Fact]
    public void ANameTheUserTypedIsNeverReplaced()
    {
        var provider = Open(new DialogParameters<TemplateValueConfigurationDialog>());

        InputFor(provider, nameof(DtoTemplateValueConfigurationBase.Name)).Change("Roof");
        ChooseDevice(provider, TemplateValueGatherType.SmaInverterModbus);
        EnterHostAndSave(provider);

        Assert.Equal("Roof", _saved?.Name);
    }

    [Fact]
    public void ANameTypedAfterAProposalIsKeptWhenTheDeviceChanges()
    {
        var provider = Open(new DialogParameters<TemplateValueConfigurationDialog>());

        ChooseDevice(provider, TemplateValueGatherType.SmaInverterModbus);
        InputFor(provider, nameof(DtoTemplateValueConfigurationBase.Name)).Change("Roof");
        ChooseDevice(provider, TemplateValueGatherType.SmaHybridInverterModbus);
        EnterHostAndSave(provider);

        Assert.Equal("Roof", _saved?.Name);
    }

    [Fact]
    public void AddingADeviceInSetupStartsFromAButtonAndAsksForTheBrand()
    {
        //The search field used to keep showing what was typed after the device had been added.
        var provider = Render<MudDialogProvider>();
        var picker = Render<SetupEquipmentSourcePicker>(parameters => parameters
            .Add(p => p.Configurations, new List<DtoValueConfigurationOverview>()));
        Assert.Empty(picker.FindComponents<MudAutocomplete<TemplateValueGatherType?>>());

        //Not awaited: the click waits for the dialog to be closed, and this test only looks at it while it is open.
        _ = picker.InvokeAsync(() => ButtonWithText(picker, "Add device").Click());

        provider.WaitForAssertion(() =>
        {
            Assert.Single(provider.FindComponents<MudSelect<string>>());
            Assert.Single(provider.FindComponents<MudSelect<TemplateValueGatherType?>>());
            Assert.False(AsksFor(provider, nameof(DtoTemplateValueConfigurationBase.Name)));
            Assert.Contains("Add device", provider.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ChangingAConnectedDeviceInSetupDoesNotAskForTheDeviceAgain()
    {
        var provider = Render<MudDialogProvider>();
        var picker = Render<SetupEquipmentSourcePicker>(parameters => parameters
            .Add(p => p.Configurations, new List<DtoValueConfigurationOverview> { new("Garage inverter") { Id = 7, }, }));

        _ = picker.InvokeAsync(() => ButtonWithText(picker, "Change").Click());

        provider.WaitForAssertion(() =>
        {
            Assert.Single(provider.FindComponents<SmaInverterEditForm>());
            Assert.Empty(provider.FindComponents<MudSelect<string>>());
            Assert.Empty(provider.FindComponents<MudSelect<TemplateValueGatherType?>>());
            Assert.False(AsksFor(provider, nameof(DtoTemplateValueConfigurationBase.Name)));
        });
    }

    private IRenderedComponent<MudDialogProvider> AddFromSetup() =>
        Open(new DialogParameters<TemplateValueConfigurationDialog> { { x => x.ShowName, false }, });

    private IRenderedComponent<MudDialogProvider> Open(DialogParameters<TemplateValueConfigurationDialog> parameters)
    {
        var provider = Render<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();
        provider.InvokeAsync(() => dialogService.ShowAsync<TemplateValueConfigurationDialog>("Device", parameters))
            .GetAwaiter().GetResult();
        provider.WaitForAssertion(() => Assert.Single(provider.FindComponents<TemplateValueConfigurationDialog>()));
        return provider;
    }

    private static IRenderedComponent<MudSelect<string>> BrandSelect(IRenderedComponent<MudDialogProvider> provider) =>
        provider.FindComponent<MudSelect<string>>();

    private static IRenderedComponent<MudSelect<TemplateValueGatherType?>> DeviceSelect(IRenderedComponent<MudDialogProvider> provider) =>
        provider.FindComponent<MudSelect<TemplateValueGatherType?>>();

    private static void ChooseBrand(IRenderedComponent<MudDialogProvider> provider, string brand)
    {
        var select = BrandSelect(provider);
        provider.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(brand)).GetAwaiter().GetResult();
    }

    private static void ChooseDevice(IRenderedComponent<MudDialogProvider> provider, TemplateValueGatherType type)
    {
        ChooseBrand(provider, TemplateValueGatherTypeVendors.GetVendor(type));
        var select = DeviceSelect(provider);
        provider.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(type)).GetAwaiter().GetResult();
    }

    private void EnterHostAndSave(IRenderedComponent<MudDialogProvider> provider)
    {
        InputFor(provider, nameof(DtoSmaInverterTemplateValueConfiguration.Host)).Change("192.168.1.50");
        ClickSave(provider);
    }

    private static AngleSharp.Dom.IElement SaveButton(IRenderedComponent<MudDialogProvider> provider) =>
        provider.FindAll("button").Last(b => b.TextContent.Contains("Save", StringComparison.OrdinalIgnoreCase));

    private void ClickSave(IRenderedComponent<MudDialogProvider> provider)
    {
        SaveButton(provider).Click();
        provider.WaitForAssertion(() => Assert.NotNull(_saved));
    }

    private static AngleSharp.Dom.IElement ButtonWithText<TComponent>(IRenderedComponent<TComponent> component, string text)
        where TComponent : Microsoft.AspNetCore.Components.IComponent =>
        component.FindAll("button").Single(b => b.TextContent.Trim() == text);

    private static bool AsksFor(IRenderedComponent<MudDialogProvider> provider, string propertyName) =>
        FindInputs(provider, propertyName).Any();

    private static AngleSharp.Dom.IElement InputFor(IRenderedComponent<MudDialogProvider> provider, string propertyName) =>
        Assert.Single(FindInputs(provider, propertyName)).Find("input");

    private static IEnumerable<IRenderedComponent<GenericInput<string>>> FindInputs(
        IRenderedComponent<MudDialogProvider> provider, string propertyName) =>
        provider.FindComponents<GenericInput<string>>()
            .Where(input => input.Instance.For?.Body is MemberExpression member && member.Member.Name == propertyName);
}
