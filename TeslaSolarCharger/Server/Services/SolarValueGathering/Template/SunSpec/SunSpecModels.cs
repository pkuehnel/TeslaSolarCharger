using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.SunSpec;

/// <summary>
/// SunSpec point definitions for the models TSC needs (power + state of charge only, since TSC does not consume
/// per phase or energy values). Offsets are 0-based register offsets from the start of the model data block, i.e.
/// after the two register ID + length header. Transcribed from the official SunSpec smdx model definitions
/// (https://github.com/sunspec/models).
/// </summary>
public static class SunSpecModels
{
    public const int ScaleFactorMarkerNaN = int.MinValue;

    /// <summary>
    /// Register value that marks a scale factor / value as not implemented.
    /// </summary>
    public const int NotImplementedInt16 = -32768;

    public static IReadOnlyDictionary<int, SunSpecModel> Models { get; } = BuildModels();

    private static Dictionary<int, SunSpecModel> BuildModels()
    {
        var models = new Dictionary<int, SunSpecModel>();

        //Inverter models with integer values and scale factors (single/split/three phase share the layout)
        var inverterIntSf = new SunSpecModel(new()
        {
            { "W", new(12, SunSpecPointValueType.Int16, scaleFactorOffset: 13) },
            { "DCW", new(29, SunSpecPointValueType.Int16, scaleFactorOffset: 30) },
        });
        models[101] = inverterIntSf;
        models[102] = inverterIntSf;
        models[103] = inverterIntSf;

        //Inverter models with float values (no scale factors)
        var inverterFloat = new SunSpecModel(new()
        {
            { "W", new(20, SunSpecPointValueType.Float32) },
            { "DCW", new(36, SunSpecPointValueType.Float32) },
        });
        models[111] = inverterFloat;
        models[112] = inverterFloat;
        models[113] = inverterFloat;

        //Meter models with integer values and scale factors
        var meterIntSf = new SunSpecModel(new()
        {
            { "W", new(16, SunSpecPointValueType.Int16, scaleFactorOffset: 20) },
        });
        models[201] = meterIntSf;
        models[202] = meterIntSf;
        models[203] = meterIntSf;

        //Meter models with float values
        var meterFloat = new SunSpecModel(new()
        {
            { "W", new(26, SunSpecPointValueType.Float32) },
        });
        models[211] = meterFloat;
        models[212] = meterFloat;
        models[213] = meterFloat;

        //Storage model (control + state of charge)
        models[124] = new SunSpecModel(new()
        {
            { "StorCtl_Mod", new(3, SunSpecPointValueType.UInt16) },
            { "ChaState", new(6, SunSpecPointValueType.UInt16, scaleFactorOffset: 20) },
            { "OutWRte", new(10, SunSpecPointValueType.Int16, scaleFactorOffset: 23) },
            { "InWRte", new(11, SunSpecPointValueType.Int16, scaleFactorOffset: 23) },
            { "InOutWRte_RvrtTms", new(13, SunSpecPointValueType.UInt16) },
            { "ChaGriSet", new(15, SunSpecPointValueType.UInt16) },
        });

        //Battery base model
        models[802] = new SunSpecModel(new()
        {
            { "SoC", new(9, SunSpecPointValueType.UInt16, scaleFactorOffset: 54) },
            { "W", new(45, SunSpecPointValueType.Int16, scaleFactorOffset: 61) },
        });

        return models;
    }

    /// <summary>
    /// Multiple MPPT inverter extension model. Uses a fixed header followed by repeating module blocks.
    /// </summary>
    public static class Model160
    {
        public const int ModelId = 160;
        public const int FixedHeaderLength = 8;
        //Scale factor offsets within the fixed header (data block relative)
        public const int DcwScaleFactorOffset = 2;
        public const int DcwhScaleFactorOffset = 3;
        public const int ModuleCountOffset = 6;
        //Standard smdx repeating block is 20 registers (ID + 8 register IDStr + values)
        public const int DefaultBlockLength = 20;

        public static int DcwOffsetInBlock(int blockLength) => blockLength >= DefaultBlockLength ? 11 : 4;
        public static int DcwhOffsetInBlock(int blockLength) => blockLength >= DefaultBlockLength ? 12 : 5;
    }
}

public enum SunSpecPointValueType
{
    Int16,
    UInt16,
    Int32,
    UInt32,
    Float32,
}

public class SunSpecPoint
{
    public SunSpecPoint(int offset, SunSpecPointValueType valueType, int? scaleFactorOffset = null)
    {
        Offset = offset;
        ValueType = valueType;
        ScaleFactorOffset = scaleFactorOffset;
    }

    public int Offset { get; }
    public SunSpecPointValueType ValueType { get; }
    /// <summary>
    /// Data block relative offset of the scale factor point (sunssf, int16), or null if the point has no scale factor.
    /// </summary>
    public int? ScaleFactorOffset { get; }

    public int RegisterCount => ValueType is SunSpecPointValueType.Int32 or SunSpecPointValueType.UInt32 or SunSpecPointValueType.Float32 ? 2 : 1;

    public ModbusValueType ToModbusValueType() => ValueType switch
    {
        SunSpecPointValueType.Int16 => ModbusValueType.Short,
        SunSpecPointValueType.UInt16 => ModbusValueType.UShort,
        SunSpecPointValueType.Int32 => ModbusValueType.Int,
        SunSpecPointValueType.UInt32 => ModbusValueType.UInt,
        SunSpecPointValueType.Float32 => ModbusValueType.Float,
        _ => throw new ArgumentOutOfRangeException(nameof(ValueType), ValueType, "Unknown SunSpec point value type"),
    };
}

public class SunSpecModel
{
    public SunSpecModel(Dictionary<string, SunSpecPoint> points)
    {
        Points = points;
    }

    public Dictionary<string, SunSpecPoint> Points { get; }
}
