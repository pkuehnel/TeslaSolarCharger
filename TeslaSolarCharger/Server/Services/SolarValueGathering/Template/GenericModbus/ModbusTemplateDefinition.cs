using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.GenericModbus;

public class ModbusTemplateDefinition
{
    public required ModbusEndianess Endianess { get; init; }
    public int ReadTimeoutMilliseconds { get; init; } = 2000;
    public int ConnectDelayMilliseconds { get; init; }
    public required List<ModbusTemplateRegister> ValueRegisters { get; init; }
    public ModbusBatteryControlDefinition? BatteryControl { get; init; }
}

public class ModbusTemplateRegister
{
    public required ModbusRegisterType RegisterType { get; init; }
    public required ModbusValueType ValueType { get; init; }
    public required int Address { get; init; }
    public required int Length { get; init; }
    public required ValueUsage UsedFor { get; init; }
    public ValueOperator Operator { get; init; } = ValueOperator.Plus;
    public decimal CorrectionFactor { get; init; } = 1;
    /// <summary>
    /// Added to the raw register value before the correction factor is applied, e.g. for devices reporting
    /// values with a fixed offset.
    /// </summary>
    public decimal Offset { get; init; }
    /// <summary>
    /// Raw register value (before applying offset and correction factor) that marks the value as currently not
    /// available, e.g. 0x8000... for signed and 0xFF... for unsigned "NaN" encodings.
    /// </summary>
    public decimal? NotAvailableValue { get; init; }
    /// <summary>
    /// Some devices expose different data behind different Modbus unit identifiers on the same connection.
    /// </summary>
    public int? UnitIdOverride { get; init; }
}

public class ModbusBatteryControlDefinition
{
    /// <summary>
    /// If set, non normal modes are rewritten periodically because the device falls back to its default behavior
    /// when the setpoints are not refreshed.
    /// </summary>
    public TimeSpan? RewriteInterval { get; init; }
    public required List<ModbusBatteryModeWrite> NormalWrites { get; init; }
    public required List<ModbusBatteryModeWrite> HoldWrites { get; init; }
    public required List<ModbusBatteryModeWrite> ChargeWrites { get; init; }

    public List<ModbusBatteryModeWrite> GetWrites(HomeBatteryMode mode)
    {
        var writes = mode switch
        {
            HomeBatteryMode.Normal => NormalWrites,
            HomeBatteryMode.Hold => HoldWrites,
            HomeBatteryMode.Charge => ChargeWrites,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Mode can not be written"),
        };
        //An empty write list marks the mode as not supported by the device, e.g. devices that can only block discharging.
        if (writes.Count == 0)
        {
            throw new NotSupportedException($"Mode {mode} is not supported by this device");
        }
        return writes;
    }
}

public enum BatteryModeWriteValueSource
{
    Constant,
    MaxChargePowerW,
    MaxDischargePowerW,
    /// <summary>
    /// Global HomeBatteryMinSoc configuration, fallback 10 when not configured.
    /// </summary>
    MinSoc,
    /// <summary>
    /// Global HomeBatteryMaxChargeSoc configuration.
    /// </summary>
    MaxChargeSoc,
    /// <summary>
    /// Current home battery soc, clamped between min soc and 100. Used for reserve based hold implementations.
    /// </summary>
    CurrentSoc,
    /// <summary>
    /// Random positive 16 bit value. Some devices require a changing trigger value so setpoint writes are applied.
    /// </summary>
    Random,
}

public class ModbusBatteryModeWrite
{
    public required int Address { get; init; }
    public required ModbusValueType ValueType { get; init; }
    public required ModbusWriteFunction WriteFunction { get; init; }
    public required BatteryModeWriteValueSource Source { get; init; }
    public decimal ConstantValue { get; init; }
    /// <summary>
    /// Factor applied to the resolved value before writing, e.g. 0.1 when the register expects 10 W units.
    /// </summary>
    public decimal Factor { get; init; } = 1;

    public static ModbusBatteryModeWrite Constant(int address, decimal value, ModbusWriteFunction writeFunction,
        ModbusValueType valueType = ModbusValueType.UShort)
    {
        return new()
        {
            Address = address,
            ValueType = valueType,
            WriteFunction = writeFunction,
            Source = BatteryModeWriteValueSource.Constant,
            ConstantValue = value,
        };
    }

    public static ModbusBatteryModeWrite Dynamic(int address, BatteryModeWriteValueSource source, ModbusWriteFunction writeFunction,
        ModbusValueType valueType = ModbusValueType.UShort, decimal factor = 1)
    {
        return new()
        {
            Address = address,
            ValueType = valueType,
            WriteFunction = writeFunction,
            Source = source,
            Factor = factor,
        };
    }
}
