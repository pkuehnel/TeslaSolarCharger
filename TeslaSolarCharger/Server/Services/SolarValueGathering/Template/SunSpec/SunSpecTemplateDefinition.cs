using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.SunSpec;

public class SunSpecTemplateDefinition
{
    public required List<SunSpecValueRead> ValueReads { get; init; }
    public SunSpecBatteryControlDefinition? BatteryControl { get; init; }
}

/// <summary>
/// One resulting value (e.g. GridPower). Made up of one or more components that are summed. Each component tries a
/// list of point references in order and uses the first one whose model exists on the device (SunSpec devices
/// implement either the integer+scalefactor or the float variant of a model).
/// </summary>
public class SunSpecValueRead
{
    public required ValueUsage UsedFor { get; init; }
    public required List<SunSpecValueComponent> Components { get; init; }
}

public class SunSpecValueComponent
{
    /// <summary>
    /// Point references tried in order, e.g. ["203:W", "213:W", "201:W", "211:W"].
    /// </summary>
    public required List<string> PointFallbacks { get; init; }
    public ValueOperator Operator { get; init; } = ValueOperator.Plus;
    /// <summary>
    /// When true a missing point (e.g. an unused MPPT string) contributes 0 instead of failing the whole value.
    /// </summary>
    public bool OptionalIfMissing { get; init; }
}

public class SunSpecBatteryControlDefinition
{
    public TimeSpan? RewriteInterval { get; init; }
    public required List<SunSpecBatteryModeWrite> NormalWrites { get; init; }
    public required List<SunSpecBatteryModeWrite> HoldWrites { get; init; }
    public required List<SunSpecBatteryModeWrite> ChargeWrites { get; init; }

    public List<SunSpecBatteryModeWrite> GetWrites(HomeBatteryMode mode)
    {
        var writes = mode switch
        {
            HomeBatteryMode.Normal => NormalWrites,
            HomeBatteryMode.Hold => HoldWrites,
            HomeBatteryMode.Charge => ChargeWrites,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Mode can not be written"),
        };
        if (writes.Count == 0)
        {
            throw new NotSupportedException($"Mode {mode} is not supported by this device");
        }
        return writes;
    }
}

public enum SunSpecWriteValueSource
{
    Constant,
    /// <summary>
    /// Negative of the configured max charge rate in percent (for model 124 OutWRte charge).
    /// </summary>
    NegativeMaxChargeRatePercent,
    /// <summary>
    /// Negative of the configured max charge power in watts (for plain register charge setpoints).
    /// </summary>
    NegativeMaxChargePowerW,
}

public class SunSpecBatteryModeWrite
{
    /// <summary>
    /// SunSpec point reference (e.g. "124:0:StorCtl_Mod"). Set this or <see cref="PlainRegisterAddress"/>.
    /// </summary>
    public string? SunSpecPointReference { get; init; }
    /// <summary>
    /// Plain (non SunSpec) holding register address for devices whose control uses vendor registers (e.g. Kostal).
    /// </summary>
    public int? PlainRegisterAddress { get; init; }
    public ModbusValueType PlainRegisterValueType { get; init; }
    public ModbusEndianess PlainRegisterEndianess { get; init; } = ModbusEndianess.BigEndian;
    public required ModbusWriteFunction WriteFunction { get; init; }
    public required SunSpecWriteValueSource Source { get; init; }
    public decimal ConstantValue { get; init; }

    public static SunSpecBatteryModeWrite Point(string pointReference, decimal value, ModbusWriteFunction writeFunction = ModbusWriteFunction.WriteMultipleRegisters)
        => new() { SunSpecPointReference = pointReference, ConstantValue = value, WriteFunction = writeFunction, Source = SunSpecWriteValueSource.Constant };

    public static SunSpecBatteryModeWrite PointNegativeChargeRate(string pointReference, ModbusWriteFunction writeFunction = ModbusWriteFunction.WriteMultipleRegisters)
        => new() { SunSpecPointReference = pointReference, WriteFunction = writeFunction, Source = SunSpecWriteValueSource.NegativeMaxChargeRatePercent };

    public static SunSpecBatteryModeWrite PlainConstant(int address, ModbusValueType valueType, decimal value, ModbusEndianess endianess, ModbusWriteFunction writeFunction = ModbusWriteFunction.WriteMultipleRegisters)
        => new() { PlainRegisterAddress = address, PlainRegisterValueType = valueType, ConstantValue = value, PlainRegisterEndianess = endianess, WriteFunction = writeFunction, Source = SunSpecWriteValueSource.Constant };

    public static SunSpecBatteryModeWrite PlainNegativeChargePower(int address, ModbusValueType valueType, ModbusEndianess endianess, ModbusWriteFunction writeFunction = ModbusWriteFunction.WriteMultipleRegisters)
        => new() { PlainRegisterAddress = address, PlainRegisterValueType = valueType, PlainRegisterEndianess = endianess, WriteFunction = writeFunction, Source = SunSpecWriteValueSource.NegativeMaxChargePowerW };
}
