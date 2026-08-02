using System;
using TeslaSolarCharger.BleApi.Native;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.BleApi;

/// <summary>
/// Decoding of the kernel's hci_dev_list_req / hci_dev_info structures. The layout is byte identical on amd64, arm64
/// and arm/v7, so fixture bytes are enough to guard it and the tests run on any development machine. Real byte
/// captures from the target hardware belong here as further fixtures.
/// </summary>
public class HciNativeTests
{
    [Fact]
    public void DecodesTheDeviceListHeaderAndEntries()
    {
        var buffer = new byte[4 + 16 * 8];
        BitConverter.GetBytes((ushort)2).CopyTo(buffer, 0);
        //Two hci_dev_req entries of 8 bytes each, first at offset 4.
        BitConverter.GetBytes((ushort)0).CopyTo(buffer, 4);
        BitConverter.GetBytes((ushort)1).CopyTo(buffer, 12);

        var deviceIds = HciNative.DecodeDeviceList(buffer);

        Assert.Equal(new[] { 0, 1 }, deviceIds);
    }

    [Fact]
    public void DecodesAnOnboardUartAdapterThatIsUp()
    {
        //Address bytes are stored least significant first, so the human readable form is reversed.
        var buffer = BuildDeviceInfo(deviceId: 0, name: "hci0",
            addressLeastSignificantFirst: new byte[] { 0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA },
            flags: 0x5, busType: 3);

        var info = HciNative.DecodeDeviceInfo(buffer);

        Assert.Equal(0, info.DeviceId);
        Assert.Equal("hci0", info.Name);
        Assert.Equal("AA:BB:CC:DD:EE:FF", info.Address);
        Assert.Equal(3, info.BusType);
        Assert.True(info.IsUp);
        Assert.True(info.IsRunning);
    }

    [Fact]
    public void DecodesAUsbAdapterThatIsDown()
    {
        var buffer = BuildDeviceInfo(deviceId: 1, name: "hci1",
            addressLeastSignificantFirst: new byte[] { 0x06, 0x05, 0x04, 0x03, 0x02, 0x01 },
            flags: 0x0, busType: 1);

        var info = HciNative.DecodeDeviceInfo(buffer);

        Assert.Equal(1, info.DeviceId);
        Assert.Equal("hci1", info.Name);
        Assert.Equal("01:02:03:04:05:06", info.Address);
        Assert.Equal(1, info.BusType);
        Assert.False(info.IsUp);
        Assert.False(info.IsRunning);
    }

    [Fact]
    public void AnAllZeroAddressIsReportedAsUnknown()
    {
        //The kernel only caches the BD address once the adapter has been up; before that it reports zeros, which must
        //never be offered as a stable identifier a car could be pinned to.
        var buffer = BuildDeviceInfo(deviceId: 2, name: "hci2",
            addressLeastSignificantFirst: new byte[6], flags: 0x0, busType: 1);

        var info = HciNative.DecodeDeviceInfo(buffer);

        Assert.Null(info.Address);
    }

    [Fact]
    public void OnlyTheLowNibbleOfTypeIsTheBus()
    {
        //The high nibble carries the device type (BR/EDR vs AMP) and must not leak into the bus.
        var buffer = BuildDeviceInfo(deviceId: 0, name: "hci0",
            addressLeastSignificantFirst: new byte[] { 1, 2, 3, 4, 5, 6 }, flags: 0x1, busType: 1, deviceTypeHighNibble: 0x1);

        var info = HciNative.DecodeDeviceInfo(buffer);

        Assert.Equal(1, info.BusType);
    }

    private static byte[] BuildDeviceInfo(int deviceId, string name, byte[] addressLeastSignificantFirst, uint flags,
        int busType, int deviceTypeHighNibble = 0)
    {
        var buffer = new byte[HciNative.DevInfoSize];
        BitConverter.GetBytes((ushort)deviceId).CopyTo(buffer, 0);
        System.Text.Encoding.ASCII.GetBytes(name).CopyTo(buffer, 2);
        addressLeastSignificantFirst.CopyTo(buffer, 10);
        BitConverter.GetBytes(flags).CopyTo(buffer, 16);
        buffer[20] = (byte)((deviceTypeHighNibble << 4) | busType);
        return buffer;
    }
}
