using Autofac;
using LanguageExt;
using Moq;
using PkSoftwareService.Custom.Backend.Ble;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

/// <summary>
/// Guards the adapter selection rule of the car configuration: saving a car pinned to an adapter the container does
/// not have would make every BLE request for that car fail with AdapterNotFound, so it is caught on save instead.
/// </summary>
public class CarBasicConfigurationValidatorTests : TestBase
{
    private const string BleUrl = "http://ble-container:7210";
    private const string OnboardAddress = "AA:BB:CC:DD:EE:FF";
    private const string DongleAddress = "01:02:03:04:05:06";

    public CarBasicConfigurationValidatorTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public async Task AcceptsAnAdapterTheContainerReports()
    {
        SetupContainer(OnboardAddress, DongleAddress);

        var result = await Validate(DongleAddress);

        Assert.DoesNotContain(result.Errors, e => e.PropertyName == nameof(CarBasicConfiguration.BleAdapterAddress));
    }

    [Fact]
    public async Task RejectsAnAdapterTheContainerDoesNotHave()
    {
        SetupContainer(OnboardAddress);

        var result = await Validate(DongleAddress);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CarBasicConfiguration.BleAdapterAddress));
    }

    [Fact]
    public async Task AcceptsTheContainerDefault()
    {
        SetupContainer(OnboardAddress);

        var result = await Validate(null);

        Assert.DoesNotContain(result.Errors, e => e.PropertyName == nameof(CarBasicConfiguration.BleAdapterAddress));
    }

    [Fact]
    public async Task DoesNotComplainWhenTheContainerCanNotBeReached()
    {
        //An empty list means the container is unreachable or outdated, which the URL rule already reports. Adding a
        //second error about the adapter would only be confusing, and would block saving a valid configuration while
        //the container happens to be down.
        SetupContainer();

        var result = await Validate(DongleAddress);

        Assert.DoesNotContain(result.Errors, e => e.PropertyName == nameof(CarBasicConfiguration.BleAdapterAddress));
    }

    private void SetupContainer(params string[] adapterAddresses)
    {
        Mock.Mock<IBleService>().Setup(b => b.GetAdapters(BleUrl))
            .ReturnsAsync(adapterAddresses.Select(address => new DtoBleAdapter
            {
                StableId = address,
                AddressKnown = true,
                Name = "hci0",
                Bus = "usb",
            }).ToList());
        //Keep the other rules quiet so only the adapter rule can produce a failure for this property.
        Mock.Mock<IBleService>().Setup(b => b.CheckBleApiVersionCompatibility(It.IsAny<string?>()))
            .ReturnsAsync((string?)null);
        Mock.Mock<ITokenHelper>().Setup(t => t.GetFleetApiTokenState(It.IsAny<bool>()))
            .ReturnsAsync(TokenState.UpToDate);
        Mock.Mock<IBackendApiService>().Setup(b => b.IsFleetApiLicensed(It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.GetVehicleDataFromTesla()).Returns(true);
    }

    private Task<FluentValidation.Results.ValidationResult> Validate(string? bleAdapterAddress)
    {
        var validator = Mock.Create<TeslaSolarCharger.Server.ServerValidators.CarBasicConfigurationValidator>();
        return validator.ValidateAsync(new CarBasicConfiguration(1, "Test Car")
        {
            Vin = "TESTVIN123456789A",
            CarType = CarType.Tesla,
            ShouldBeManaged = true,
            UseBle = true,
            BleApiBaseUrl = BleUrl,
            BleAdapterAddress = bleAdapterAddress,
            UseFleetTelemetry = true,
            MinimumAmpere = 6,
            MaximumAmpere = 16,
            UsableEnergy = 75,
            ChargingPriority = 1,
            MaximumPhases = 3,
        });
    }
}
