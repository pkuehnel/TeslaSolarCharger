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
    /// Raw register value (before applying the correction factor) that marks the value as currently not available,
    /// e.g. 0x8000... for signed and 0xFF... for unsigned "NaN" encodings.
    /// </summary>
    public decimal? NotAvailableValue { get; init; }
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
        return mode switch
        {
            HomeBatteryMode.Normal => NormalWrites,
            HomeBatteryMode.Hold => HoldWrites,
            HomeBatteryMode.Charge => ChargeWrites,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Mode can not be written"),
        };
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
