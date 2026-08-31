using PkSoftwareService.Custom.Backend.Ble;
using TeslaSolarCharger.BleApi.Native;
using TeslaSolarCharger.BleApi.Services.Contracts;

namespace TeslaSolarCharger.BleApi.Services;

public class AdapterEnumerationService(ILogger<AdapterEnumerationService> logger,
    IConfiguration configuration,
    TimeProvider timeProvider) : IAdapterEnumerationService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);
    private readonly object _cacheLock = new();
    private List<DtoBleAdapter>? _cachedAdapters;
    private DateTimeOffset _cacheTimestamp;

    //Virtual so tests can supply an adapter list without the HCI syscalls, which only exist on a Linux host.
    public virtual List<DtoBleAdapter> GetAdapters(bool bypassCache = false)
    {
        lock (_cacheLock)
        {
            var now = timeProvider.GetUtcNow();
            if (!bypassCache && _cachedAdapters != default && now - _cacheTimestamp < CacheDuration)
            {
                return _cachedAdapters;
            }
            _cachedAdapters = BuildAdapters();
            _cacheTimestamp = now;
            return _cachedAdapters;
        }
    }

    public AdapterResolution Resolve(string? requestedStableId)
    {
        var adapters = GetAdapters();
        if (!string.IsNullOrWhiteSpace(requestedStableId))
        {
            var requested = requestedStableId.Trim().ToUpperInvariant();
            var match = adapters.FirstOrDefault(a => a.AddressKnown && string.Equals(a.StableId, requested, StringComparison.OrdinalIgnoreCase));
            if (match == default)
            {
                //Never fall back to a different radio: a missing configured adapter is an explicit, visible error.
                return new AdapterResolution { Found = false, Key = requested, IsExplicit = true };
            }
            return new AdapterResolution { Found = true, Key = match.StableId!, HciId = match.Name, IsExplicit = true };
        }

        //Default resolution keeps today's semantics: the BluetoothAdapter env value if set, else the first adapter.
        //The key is still the BD address whenever it is known, so an explicit selection of the same physical adapter
        //shares the worker instance with default requests.
        var configuredAdapter = configuration.GetValue<string>("BluetoothAdapter");
        if (!string.IsNullOrWhiteSpace(configuredAdapter))
        {
            var configured = adapters.FirstOrDefault(a => string.Equals(a.Name, configuredAdapter, StringComparison.OrdinalIgnoreCase));
            if (configured != default && configured.AddressKnown)
            {
                return new AdapterResolution { Found = true, Key = configured.StableId!, HciId = configured.Name };
            }
            return new AdapterResolution { Found = true, Key = $"hci:{configuredAdapter}", HciId = configuredAdapter };
        }
        var first = adapters.FirstOrDefault();
        if (first == default)
        {
            //Enumeration unavailable (development machine, missing Bluetooth support): let the worker pick the first
            //adapter itself, exactly like tesla-control without -bt-adapter does today.
            return new AdapterResolution { Found = true, Key = "default", HciId = string.Empty };
        }
        if (first.AddressKnown)
        {
            return new AdapterResolution { Found = true, Key = first.StableId!, HciId = first.Name };
        }
        return new AdapterResolution { Found = true, Key = $"hci:{first.Name}", HciId = first.Name };
    }

    private List<DtoBleAdapter> BuildAdapters()
    {
        var adapters = new List<DtoBleAdapter>();
        List<HciDeviceInfo> devices;
        try
        {
            devices = HciNative.QueryDevices();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not enumerate Bluetooth adapters");
            return adapters;
        }
        foreach (var device in devices.OrderBy(d => d.DeviceId))
        {
            var adapter = new DtoBleAdapter
            {
                StableId = device.Address,
                AddressKnown = device.Address != default,
                HciIndex = device.DeviceId,
                Name = device.Name,
                Bus = BusName(device),
                UsbProduct = ReadUsbProduct(device.Name),
                State = DetermineState(device),
            };
            adapters.Add(adapter);
        }
        //Duplicate addresses (cheap clone dongles) are not usable as identifiers.
        foreach (var duplicates in adapters.Where(a => a.AddressKnown).GroupBy(a => a.StableId).Where(g => g.Count() > 1))
        {
            logger.LogWarning("Multiple adapters report the BD address {address}; they can not be selected individually", duplicates.Key);
            foreach (var duplicate in duplicates)
            {
                duplicate.AddressKnown = false;
                duplicate.StableId = null;
            }
        }
        MarkDefaultAdapter(adapters);
        return adapters;
    }

    private BleAdapterState DetermineState(HciDeviceInfo device)
    {
        var (softBlocked, hardBlocked) = ReadRfkillState(device.Name);
        if (softBlocked || hardBlocked)
        {
            return BleAdapterState.Blocked;
        }
        //A worker holding the exclusive user channel leaves the device down from the kernel's perspective; the
        //caller overlays OwnedByWorker over Down using its worker registry.
        return device.IsUp ? BleAdapterState.Up : BleAdapterState.Down;
    }

    private string BusName(HciDeviceInfo device)
    {
        var busName = device.BusType switch
        {
            1 => "usb",
            3 => "uart",
            _ => null,
        };
        if (busName != default)
        {
            return busName;
        }
        //Sysfs fallback: /sys/class/bluetooth/hciX/device/subsystem links to "usb" or "serial".
        try
        {
            var subsystemPath = $"/sys/class/bluetooth/{device.Name}/device/subsystem";
            var target = new DirectoryInfo(subsystemPath).LinkTarget;
            if (!string.IsNullOrEmpty(target))
            {
                var subsystem = Path.GetFileName(target.TrimEnd('/'));
                return subsystem == "serial" ? "uart" : subsystem;
            }
        }
        catch (Exception ex)
        {
            logger.LogTrace(ex, "Could not read the bus type of {adapter} from sysfs", device.Name);
        }
        return device.BusType.ToString();
    }

    private string? ReadUsbProduct(string adapterName)
    {
        try
        {
            //The device link of a USB adapter points to the USB interface; the product string lives on its parent.
            var productPath = $"/sys/class/bluetooth/{adapterName}/device/../product";
            if (File.Exists(productPath))
            {
                var product = File.ReadAllText(productPath).Trim();
                return string.IsNullOrEmpty(product) ? null : product;
            }
        }
        catch (Exception ex)
        {
            logger.LogTrace(ex, "Could not read the USB product of {adapter} from sysfs", adapterName);
        }
        return null;
    }

    private (bool SoftBlocked, bool HardBlocked) ReadRfkillState(string adapterName)
    {
        try
        {
            const string rfkillRoot = "/sys/class/rfkill";
            if (!Directory.Exists(rfkillRoot))
            {
                return (false, false);
            }
            foreach (var entry in Directory.EnumerateDirectories(rfkillRoot))
            {
                var namePath = Path.Combine(entry, "name");
                if (!File.Exists(namePath) || File.ReadAllText(namePath).Trim() != adapterName)
                {
                    continue;
                }
                var softBlocked = File.Exists(Path.Combine(entry, "soft")) && File.ReadAllText(Path.Combine(entry, "soft")).Trim() == "1";
                var hardBlocked = File.Exists(Path.Combine(entry, "hard")) && File.ReadAllText(Path.Combine(entry, "hard")).Trim() == "1";
                return (softBlocked, hardBlocked);
            }
        }
        catch (Exception ex)
        {
            logger.LogTrace(ex, "Could not read the rfkill state of {adapter}", adapterName);
        }
        return (false, false);
    }

    private void MarkDefaultAdapter(List<DtoBleAdapter> adapters)
    {
        if (adapters.Count == 0)
        {
            return;
        }
        var configuredAdapter = configuration.GetValue<string>("BluetoothAdapter");
        var defaultAdapter = string.IsNullOrWhiteSpace(configuredAdapter)
            ? adapters[0]
            : adapters.FirstOrDefault(a => string.Equals(a.Name, configuredAdapter, StringComparison.OrdinalIgnoreCase));
        if (defaultAdapter != default)
        {
            defaultAdapter.IsDefault = true;
        }
    }
}
