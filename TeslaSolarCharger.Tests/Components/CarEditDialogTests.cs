using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bunit;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using MudExtensions.Services;
using PkSoftwareService.Custom.Backend.Ble;
using TeslaSolarCharger.Client.Components;
using TeslaSolarCharger.Client.Dialogs;
using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Client.Wrapper;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Ble;
using TeslaSolarCharger.Shared.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Components;

/// <summary>
/// The car settings dialog testing its own car. The test has to run against the car in front of the user: a test
/// that asks about a different car answers a question nobody asked.
/// </summary>
public class CarEditDialogTests : Bunit.TestContext
{
    private const string Vin = "LRW3E7EK5TC770390";
    private const string BleTestButtonText = "Save and test Bluetooth connection";

    private readonly Mock<ICarSettingsService> _carSettingsService = new();
    private readonly Mock<IHomeService> _homeService = new();
    private readonly Mock<IHttpClientHelper> _httpClientHelper = new();

    private readonly List<string?> _testedVins = new();

    public CarEditDialogTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddMudExtensions();
        Services.AddSharedDependencies();
        //The same validators the app registers, so the save is refused here exactly when it would be refused there.
        Services.AddValidatorsFromAssemblyContaining<CarBasicConfigurationValidator>(ServiceLifetime.Singleton);
        Services.AddSingleton(Mock.Of<IJavaScriptWrapper>());

        _carSettingsService
            .Setup(s => s.UpdateCarBasicConfiguration(It.IsAny<int>(), It.IsAny<CarBasicConfiguration>()))
            .ReturnsAsync(new Result<object>(null, null, null));
        _carSettingsService
            .Setup(s => s.TestBleConnection(It.IsAny<string>()))
            .Callback<string>(vin => _testedVins.Add(vin))
            .ReturnsAsync(new DtoBleConnectionTestResult { ResultType = BleConnectionTestResultType.Success, });
        Services.AddSingleton(_carSettingsService.Object);

        _homeService.Setup(s => s.GetFleetApiState(It.IsAny<int>()))
            .ReturnsAsync(new Result<TeslaCarFleetApiState?>(TeslaCarFleetApiState.Ok, null, null));
        Services.AddSingleton(_homeService.Object);

        _httpClientHelper.Setup(h => h.SendGetRequestAsync<DtoValue<bool>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<DtoValue<bool>>(new DtoValue<bool>(false), null, null));
        _httpClientHelper.Setup(h => h.SendGetRequestAsync<List<DtoBleAdapter>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<List<DtoBleAdapter>>(new List<DtoBleAdapter>(), null, null));
        Services.AddSingleton(_httpClientHelper.Object);
    }

    [Fact]
    public void TestingTheBluetoothConnectionAsksAboutTheCarInTheDialog()
    {
        //The VIN used to be handed to the test component as the literal text "Car.Item.Vin" (a Razor attribute
        //without its @), so TSC looked for a car of that name and the user got "Sequence contains no matching
        //element" instead of a connection test.
        var dialog = OpenDialog();

        ClickBleTest(dialog);

        dialog.WaitForAssertion(() => Assert.Equal(new[] { Vin, }, _testedVins));
    }

    [Fact]
    public void TheConnectionTestIsShownForTheCarInTheDialog()
    {
        var dialog = OpenDialog();

        ClickBleTest(dialog);

        dialog.WaitForAssertion(() =>
            Assert.Equal(Vin, dialog.FindComponent<BleConnectionTestComponent>().Instance.Vin));
    }

    [Fact]
    public void TheCarIsSavedBeforeItIsTested()
    {
        //A BLE url typed moments ago is only tested if it was stored first, so the save has to happen before the
        //test - and the test only runs at all if the save worked.
        var dialog = OpenDialog();

        ClickBleTest(dialog);

        dialog.WaitForAssertion(() => Assert.Single(_testedVins));
        _carSettingsService.Verify(s => s.UpdateCarBasicConfiguration(3, It.Is<CarBasicConfiguration>(c => c.Vin == Vin)),
            Times.Once);
    }

    [Fact]
    public void AFailedSaveIsReportedInsteadOfTestingTheConnection()
    {
        _carSettingsService
            .Setup(s => s.UpdateCarBasicConfiguration(It.IsAny<int>(), It.IsAny<CarBasicConfiguration>()))
            .ReturnsAsync(new Result<object>(null, "Car could not be saved", null));
        var dialog = OpenDialog();

        ClickBleTest(dialog);

        dialog.WaitForAssertion(() => Assert.Empty(dialog.FindComponents<BleConnectionTestComponent>()));
        Assert.Empty(_testedVins);
    }

    private IRenderedComponent<MudDialogProvider> OpenDialog()
    {
        var car = new EditableItem<CarBasicConfiguration>(new CarBasicConfiguration(3, "Patricks M3")
        {
            Vin = Vin,
            CarType = CarType.Tesla,
            ShouldBeManaged = true,
            UseBle = true,
            BleApiBaseUrl = "http://raspis4ctest:7210/",
            MinimumAmpere = 2,
            MaximumAmpere = 16,
            MaximumPhases = 3,
            UsableEnergy = 75,
            ChargingPriority = 3,
            HomeDetectionVia = HomeDetectionVia.GpsLocation,
        });
        var provider = Render<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<CarEditDialog> { { x => x.Car, car }, };
        provider.InvokeAsync(() => dialogService.ShowAsync<CarEditDialog>("Car", parameters)).GetAwaiter().GetResult();
        provider.WaitForAssertion(() => Assert.Single(provider.FindComponents<CarEditDialog>()));
        return provider;
    }

    private static void ClickBleTest(IRenderedComponent<MudDialogProvider> dialog)
    {
        var button = dialog.FindAll("button")
            .FirstOrDefault(b => b.TextContent.Contains(BleTestButtonText, StringComparison.Ordinal));
        Assert.NotNull(button);
        button.Click();
    }
}
