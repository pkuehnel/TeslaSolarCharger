using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

namespace TeslaSolarCharger.Shared.Dtos.Setup;

/// <summary>
/// The base configuration properties the setup assistant is responsible for. Applying a setup state copies only
/// these onto the live configuration, so a value changed in Base Configuration while the assistant was open is not
/// overwritten by the assistant's older snapshot of the whole configuration.
/// </summary>
public static class SetupConfigurationOwnership
{
    public static readonly IReadOnlyList<string> OwnedProperties = new[]
    {
        nameof(BaseConfigurationBase.PredictSolarPowerGeneration),
        nameof(BaseConfigurationBase.UsePredictedSolarPowerGenerationForChargingSchedules),
        nameof(BaseConfigurationBase.ShowEnergyDataOnHome),
        nameof(BaseConfigurationBase.DynamicHomeBatteryMinSoc),
        nameof(BaseConfigurationBase.HomeBatteryMinSoc),
        nameof(BaseConfigurationBase.HomeBatteryUsableEnergy),
        nameof(BaseConfigurationBase.HomeBatteryChargingPower),
        nameof(BaseConfigurationBase.HomeBatteryDischargingPower),
        nameof(BaseConfigurationBase.HomeGeofenceLatitude),
        nameof(BaseConfigurationBase.HomeGeofenceLongitude),
        nameof(BaseConfigurationBase.HomeGeofenceRadius),
        nameof(BaseConfigurationBase.GetVehicleDataViaBle),
    };

    /// <summary>
    /// Copies the properties the assistant owns from <paramref name="from"/> onto <paramref name="onto"/> and
    /// leaves every other property of <paramref name="onto"/> untouched.
    /// </summary>
    public static void CopyOwnedProperties(DtoBaseConfiguration from, DtoBaseConfiguration onto)
    {
        var properties = typeof(DtoBaseConfiguration).GetProperties();
        foreach (var propertyName in OwnedProperties)
        {
            var property = properties.FirstOrDefault(p => p.Name == propertyName);
            if (property is not { CanRead: true, CanWrite: true })
            {
                continue;
            }

            property.SetValue(onto, property.GetValue(from));
        }
    }
}
