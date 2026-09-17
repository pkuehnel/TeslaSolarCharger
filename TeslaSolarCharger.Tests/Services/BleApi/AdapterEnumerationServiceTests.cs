using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PkSoftwareService.Custom.Backend.Ble;
using System;
using System.Collections.Generic;
using TeslaSolarCharger.BleApi.Services;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.BleApi;

/// <summary>
/// Adapter resolution decides which worker instance serves a request. Getting it wrong would let two workers fight
/// over one HCI device, or silently send a car's commands over a different radio than configured.
/// </summary>
public class AdapterEnumerationServiceTests
{
    private const string OnboardAddress = "AA:BB:CC:DD:EE:FF";
    private const string DongleAddress = "01:02:03:04:05:06";

    [Fact]
    public void ExplicitSelectionOfAnUnknownAdapterIsNotFound()
    {
        var service = CreateService(BuildAdapters());

        var resolution = service.Resolve("99:99:99:99:99:99");

        //Never fall back to another radio: the caller turns this into a visible AdapterNotFound error.
        Assert.False(resolution.Found);
        Assert.True(resolution.IsExplicit);
    }

    [Fact]
    public void ExplicitSelectionResolvesToTheCurrentHciId()
    {
        var service = CreateService(BuildAdapters());

        var resolution = service.Resolve(DongleAddress);

        Assert.True(resolution.Found);
        Assert.Equal(DongleAddress, resolution.Key);
        Assert.Equal("hci1", resolution.HciId);
    }

    [Fact]
    public void SelectionIsCaseInsensitiveAndTrimmed()
    {
        var service = CreateService(BuildAdapters());

        var resolution = service.Resolve("  01:02:03:04:05:06  ".ToLowerInvariant());

        Assert.True(resolution.Found);
        Assert.Equal(DongleAddress, resolution.Key);
    }

    [Fact]
    public void RequestWithoutAdapterUsesTheConfiguredDefaultAdapter()
    {
        var service = CreateService(BuildAdapters(), configuredAdapter: "hci1");

        var resolution = service.Resolve(null);

        Assert.True(resolution.Found);
        Assert.Equal(DongleAddress, resolution.Key);
        Assert.Equal("hci1", resolution.HciId);
        Assert.False(resolution.IsExplicit);
    }

    [Fact]
    public void DefaultAndExplicitSelectionOfTheSameAdapterShareOneWorker()
    {
        var service = CreateService(BuildAdapters(), configuredAdapter: "hci0");

        var byDefault = service.Resolve(null);
        var explicitly = service.Resolve(OnboardAddress);

        //Same key means the same WorkerInstance, so the two paths can never open the same HCI device twice.
        Assert.Equal(byDefault.Key, explicitly.Key);
    }

    [Fact]
    public void RequestWithoutAdapterAndWithoutConfigurationUsesTheFirstAdapter()
    {
        var service = CreateService(BuildAdapters());

        var resolution = service.Resolve(null);

        Assert.True(resolution.Found);
        Assert.Equal(OnboardAddress, resolution.Key);
        Assert.Equal("hci0", resolution.HciId);
    }

    [Fact]
    public void WithoutEnumerationTheDefaultRequestStillWorks()
    {
        //Enumeration is unavailable on a development machine or without Bluetooth support. The worker then picks the
        //first adapter itself, exactly like tesla-control without -bt-adapter does.
        var service = CreateService(new List<DtoBleAdapter>());

        var resolution = service.Resolve(null);

        Assert.True(resolution.Found);
        Assert.Equal("default", resolution.Key);
        Assert.Equal(string.Empty, resolution.HciId);
    }

    [Fact]
    public void AnAdapterWithUnknownAddressCanStillServeDefaultRequests()
    {
        var adapters = new List<DtoBleAdapter>
        {
            new() { Name = "hci0", HciIndex = 0, Bus = "uart", AddressKnown = false, StableId = null, },
        };
        var service = CreateService(adapters);

        var resolution = service.Resolve(null);

        Assert.True(resolution.Found);
        Assert.Equal("hci:hci0", resolution.Key);
        Assert.Equal("hci0", resolution.HciId);
    }

    [Fact]
    public void AnAdapterWithUnknownAddressCanNotBeSelectedExplicitly()
    {
        var adapters = new List<DtoBleAdapter>
        {
            new() { Name = "hci0", HciIndex = 0, Bus = "uart", AddressKnown = false, StableId = null, },
        };
        var service = CreateService(adapters);

        Assert.False(service.Resolve("00:00:00:00:00:00").Found);
    }

    private static List<DtoBleAdapter> BuildAdapters() => new()
    {
        new() { Name = "hci0", HciIndex = 0, Bus = "uart", StableId = OnboardAddress, AddressKnown = true, State = BleAdapterState.Up, },
        new() { Name = "hci1", HciIndex = 1, Bus = "usb", StableId = DongleAddress, AddressKnown = true, State = BleAdapterState.Up, },
    };

    /// <summary>
    /// Builds the service with a fixed adapter list, bypassing the HCI syscalls that only exist on a Linux host.
    /// </summary>
    private static TestableAdapterEnumerationService CreateService(List<DtoBleAdapter> adapters, string? configuredAdapter = null)
    {
        var configurationValues = new Dictionary<string, string?>();
        if (configuredAdapter != default)
        {
            configurationValues["BluetoothAdapter"] = configuredAdapter;
        }
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configurationValues).Build();
        return new TestableAdapterEnumerationService(
            Mock.Of<ILogger<AdapterEnumerationService>>(), configuration, TimeProvider.System, adapters);
    }

    private sealed class TestableAdapterEnumerationService : AdapterEnumerationService
    {
        private readonly List<DtoBleAdapter> _adapters;

        public TestableAdapterEnumerationService(ILogger<AdapterEnumerationService> logger, IConfiguration configuration,
            TimeProvider timeProvider, List<DtoBleAdapter> adapters)
            : base(logger, configuration, timeProvider)
        {
            _adapters = adapters;
        }

        public override List<DtoBleAdapter> GetAdapters(bool bypassCache = false) => _adapters;
    }
}
