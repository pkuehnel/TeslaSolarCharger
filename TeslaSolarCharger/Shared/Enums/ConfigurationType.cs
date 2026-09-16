namespace TeslaSolarCharger.Shared.Enums;

/// <summary>
/// Which kind of configuration a value comes from. Together with the configuration's id it names one device.
/// </summary>
public enum ConfigurationType
{
    RestSolarValue,
    ModbusSolarValue,
    MqttSolarValue,
    CarValue,
    OcppChargingConnectorValue,
    TemplateValue = 1000,
    /// <summary>The made-up solar values used when fake solar values are switched on.</summary>
    FakeSolarValue = 2000,
}
