using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template;

/// <summary>
/// Raw register values devices use to mark a value as currently not available (the Modbus and SunSpec "NaN"
/// encodings), e.g. SMA inverters at night.
/// </summary>
public static class NotAvailableValues
{
    public const decimal Int16 = -32768;
    public const decimal UInt16 = 65535;
    public const decimal Int32 = -2147483648;
    public const decimal UInt32 = 4294967295;

    public static bool IsNotAvailable(decimal rawValue, ModbusValueType valueType) => valueType switch
    {
        ModbusValueType.Short => rawValue == Int16,
        ModbusValueType.UShort => rawValue == UInt16,
        ModbusValueType.Int => rawValue == Int32,
        ModbusValueType.UInt => rawValue == UInt32,
        _ => false,
    };

    /// <summary>
    /// The value to use for a reading the device marked as not available. Production that is not available means the
    /// inverter currently produces nothing (e.g. at night or under snow), any other value can not be used.
    /// </summary>
    /// <exception cref="InvalidDataException">The value is not production.</exception>
    public static decimal ValueForNotAvailable(ValueUsage? usage, int address)
    {
        if (usage == ValueUsage.InverterPower)
        {
            return 0;
        }
        throw new InvalidDataException($"Register {address} returned its not available sentinel value");
    }
}
