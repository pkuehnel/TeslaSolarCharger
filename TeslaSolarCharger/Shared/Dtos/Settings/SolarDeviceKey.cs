using System.Globalization;

namespace TeslaSolarCharger.Shared.Dtos.Settings;

public readonly record struct SolarDeviceKey
{
    public SolarDeviceKey(SolarDeviceType deviceType, int configurationId, string? customIdentifier = null)
    {
        DeviceType = deviceType;
        ConfigurationId = configurationId;
        CustomIdentifier = customIdentifier;
    }

    public SolarDeviceType DeviceType { get; }

    public int ConfigurationId { get; }

    public string? CustomIdentifier { get; }

    public static SolarDeviceKey ForRest(int configurationId) => new(SolarDeviceType.Rest, configurationId);

    public static SolarDeviceKey ForModbus(int configurationId) => new(SolarDeviceType.Modbus, configurationId);

    public static SolarDeviceKey ForMqtt(int configurationId) => new(SolarDeviceType.Mqtt, configurationId);

    public static SolarDeviceKey ForFake(string identifier) => new(SolarDeviceType.Fake, 0, identifier);

    /// <summary>
    /// Returns a stable key that can be used inside configuration sources (e.g. environment variables).
    /// </summary>
    public string ToConfigurationKey()
    {
        if (string.IsNullOrWhiteSpace(CustomIdentifier))
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}_{1}", DeviceType, ConfigurationId);
        }

        return string.Format(CultureInfo.InvariantCulture, "{0}_{1}_{2}", DeviceType, ConfigurationId, CustomIdentifier);
    }

    public override string ToString()
    {
        if (string.IsNullOrWhiteSpace(CustomIdentifier))
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1}", DeviceType, ConfigurationId);
        }

        return string.Format(CultureInfo.InvariantCulture, "{0}:{1}:{2}", DeviceType, ConfigurationId, CustomIdentifier);
    }
}
