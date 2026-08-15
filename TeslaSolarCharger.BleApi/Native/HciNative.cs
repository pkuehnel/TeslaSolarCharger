using System.Runtime.InteropServices;

namespace TeslaSolarCharger.BleApi.Native;

/// <summary>
/// One Bluetooth adapter as reported by HCIGETDEVINFO. Address is null when the kernel has not cached a BD address
/// yet, which happens for adapters that have never been brought up since boot.
/// </summary>
public record HciDeviceInfo(int DeviceId, string Name, string? Address, int BusType, bool IsUp, bool IsRunning);

/// <summary>
/// Read only enumeration of the host's Bluetooth adapters over the HCI control socket, the same ioctls hciconfig
/// uses. The socket is never bound: the exclusive user channel a BLE worker holds is only ever taken by a bind() to
/// a specific device, so this enumeration can not disturb a running worker. The GET ioctls do not require
/// CAP_NET_ADMIN.
///
/// The struct layouts are byte identical on amd64, arm64 and arm/v7 (fixed size fields, no pointers, no longs), and
/// the ioctl request numbers use the generic _IOR encoding shared by those architectures. The pure decoding lives in
/// separate functions so it is unit testable with fixture bytes on any OS.
/// </summary>
public static class HciNative
{
    private const int AfBluetooth = 31;
    private const int SockRaw = 3;
    //The control socket must not leak into the worker processes this container spawns.
    private const int SockCloexec = 0x80000;
    private const int BtProtoHci = 1;
    //_IOR('H', 210, int) and _IOR('H', 211, int)
    private const uint HciGetDevListRequest = 0x800448d2;
    private const uint HciGetDevInfoRequest = 0x800448d3;
    private const int MaxDevices = 16;
    private const int DevListEntrySize = 8;
    private const int DevListHeaderSize = 4;
    public const int DevInfoSize = 92;

    [DllImport("libc", SetLastError = true)]
    private static extern int socket(int domain, int type, int protocol);

    [DllImport("libc", SetLastError = true)]
    private static extern int ioctl(int fd, nuint request, byte[] argument);

    [DllImport("libc", SetLastError = true)]
    private static extern int close(int fd);

    /// <summary>
    /// Lists the host's Bluetooth adapters. Returns an empty list when no HCI control socket is available (no
    /// Bluetooth support, insufficient permissions or a non Linux development machine).
    /// </summary>
    public static List<HciDeviceInfo> QueryDevices()
    {
        var devices = new List<HciDeviceInfo>();
        int fd;
        try
        {
            fd = socket(AfBluetooth, SockRaw | SockCloexec, BtProtoHci);
        }
        catch (DllNotFoundException)
        {
            return devices;
        }
        catch (EntryPointNotFoundException)
        {
            return devices;
        }
        if (fd < 0)
        {
            return devices;
        }
        try
        {
            var listBuffer = new byte[DevListHeaderSize + MaxDevices * DevListEntrySize];
            BitConverter.GetBytes((ushort)MaxDevices).CopyTo(listBuffer, 0);
            if (ioctl(fd, HciGetDevListRequest, listBuffer) < 0)
            {
                return devices;
            }
            foreach (var deviceId in DecodeDeviceList(listBuffer))
            {
                var infoBuffer = new byte[DevInfoSize];
                BitConverter.GetBytes((ushort)deviceId).CopyTo(infoBuffer, 0);
                if (ioctl(fd, HciGetDevInfoRequest, infoBuffer) < 0)
                {
                    continue;
                }
                devices.Add(DecodeDeviceInfo(infoBuffer));
            }
        }
        finally
        {
            _ = close(fd);
        }
        return devices;
    }

    /// <summary>
    /// Decodes a hci_dev_list_req buffer: __u16 dev_num followed by dev_num hci_dev_req entries
    /// ({ __u16 dev_id; __u32 dev_opt; }, 8 bytes each, first entry at offset 4).
    /// </summary>
    public static List<int> DecodeDeviceList(ReadOnlySpan<byte> buffer)
    {
        var deviceIds = new List<int>();
        var deviceCount = BitConverter.ToUInt16(buffer[..2]);
        for (var i = 0; i < deviceCount && DevListHeaderSize + (i + 1) * DevListEntrySize <= buffer.Length; i++)
        {
            deviceIds.Add(BitConverter.ToUInt16(buffer.Slice(DevListHeaderSize + i * DevListEntrySize, 2)));
        }
        return deviceIds;
    }

    /// <summary>
    /// Decodes a hci_dev_info buffer: __u16 dev_id; char name[8]; bdaddr_t bdaddr (6 bytes, stored least significant
    /// first); __u32 flags; __u8 type (low nibble = bus, 1 = USB, 3 = UART). An all zero bdaddr means the kernel has
    /// not read the address yet and is reported as unknown, never as a valid identifier.
    /// </summary>
    public static HciDeviceInfo DecodeDeviceInfo(ReadOnlySpan<byte> buffer)
    {
        var deviceId = BitConverter.ToUInt16(buffer[..2]);
        var nameBytes = buffer.Slice(2, 8);
        var nameLength = nameBytes.IndexOf((byte)0);
        if (nameLength < 0)
        {
            nameLength = nameBytes.Length;
        }
        var name = System.Text.Encoding.ASCII.GetString(nameBytes[..nameLength]);
        var addressBytes = buffer.Slice(10, 6);
        string? address = null;
        var addressKnown = false;
        foreach (var addressByte in addressBytes)
        {
            if (addressByte != 0)
            {
                addressKnown = true;
                break;
            }
        }
        if (addressKnown)
        {
            //bdaddr_t stores the least significant byte first; the human readable form is reversed.
            address = $"{addressBytes[5]:X2}:{addressBytes[4]:X2}:{addressBytes[3]:X2}:{addressBytes[2]:X2}:{addressBytes[1]:X2}:{addressBytes[0]:X2}";
        }
        var flags = BitConverter.ToUInt32(buffer.Slice(16, 4));
        var busType = buffer[20] & 0x0f;
        var isUp = (flags & 0x1) != 0;
        var isRunning = (flags & 0x4) != 0;
        return new HciDeviceInfo(deviceId, name, address, busType, isUp, isRunning);
    }
}
