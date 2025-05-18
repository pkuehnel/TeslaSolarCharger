using System.Text.Json;
using System.Text.Json.Serialization;
// ReSharper disable InconsistentNaming

namespace TeslaSolarCharger.Server.Dtos.Ocpp;


/// <summary>
/// Request message for MeterValues from Charge Point to Central System
/// </summary>
public class MeterValuesRequest
{
    /// <summary>
    /// Required. The ID of the connector for which meter values are reported.
    /// </summary>
    [JsonPropertyName("connectorId")]
    public int ConnectorId { get; set; }

    /// <summary>
    /// Optional. The transaction ID for which meter values are reported.
    /// </summary>
    [JsonPropertyName("transactionId")]
    public int? TransactionId { get; set; }

    /// <summary>
    /// Required. The collection of meter values.
    /// </summary>
    [JsonPropertyName("meterValue")]
    public List<MeterValue> MeterValue { get; set; }
}

/// <summary>
/// Class to hold meter value data with timestamp and sampled values
/// </summary>
public class MeterValue
{
    /// <summary>
    /// Required. Timestamp for the meter values.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Required. Collection of sampled values taken at the timestamp.
    /// </summary>
    [JsonPropertyName("sampledValue")]
    public List<SampledValue> SampledValue { get; set; }
}

/// <summary>
/// Class to hold sampled meter value details
/// </summary>
public class SampledValue
{
    /// <summary>
    /// Required. The value of the sampled value.
    /// </summary>
    [JsonPropertyName("value")]
    public string Value { get; set; }

    /// <summary>
    /// Optional. The context of the sampled value.
    /// </summary>
    [JsonPropertyName("context")]
    [JsonConverter(typeof(ReadingContextJsonConverter))]
    public ReadingContext? Context { get; set; }

    /// <summary>
    /// Optional. The format of the value.
    /// </summary>
    [JsonPropertyName("format")]
    [JsonConverter(typeof(ValueFormatJsonConverter))]
    public ValueFormat? Format { get; set; }

    /// <summary>
    /// Optional. The type of measurement.
    /// </summary>
    [JsonPropertyName("measurand")]
    [JsonConverter(typeof(MeasurandJsonConverter))]
    public Measurand? Measurand { get; set; }

    /// <summary>
    /// Optional. The phase of the measurement.
    /// </summary>
    [JsonPropertyName("phase")]
    [JsonConverter(typeof(PhaseJsonConverter))]
    public Phase? Phase { get; set; }

    /// <summary>
    /// Optional. The location of the measurement.
    /// </summary>
    [JsonPropertyName("location")]
    [JsonConverter(typeof(LocationJsonConverter))]
    public Location? Location { get; set; }

    /// <summary>
    /// Optional. The unit of the measurement.
    /// </summary>
    [JsonPropertyName("unit")]
    [JsonConverter(typeof(UnitOfMeasureJsonConverter))]
    public UnitOfMeasure? Unit { get; set; }
}

/// <summary>
/// Enumeration of context values for sampled values
/// </summary>
public enum ReadingContext
{
    InterruptionBegin,
    InterruptionEnd,
    SampleClock,
    SamplePeriodic,
    TransactionBegin,
    TransactionEnd,
    Trigger,
    Other
}

/// <summary>
/// Custom JSON converter for ReadingContext enum to handle special formatting with periods
/// </summary>
public class ReadingContextJsonConverter : JsonConverter<ReadingContext?>
{
    public override ReadingContext? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        string value = reader.GetString();
        return value switch
        {
            "Interruption.Begin" => ReadingContext.InterruptionBegin,
            "Interruption.End" => ReadingContext.InterruptionEnd,
            "Sample.Clock" => ReadingContext.SampleClock,
            "Sample.Periodic" => ReadingContext.SamplePeriodic,
            "Transaction.Begin" => ReadingContext.TransactionBegin,
            "Transaction.End" => ReadingContext.TransactionEnd,
            "Trigger" => ReadingContext.Trigger,
            "Other" => ReadingContext.Other,
            _ => throw new JsonException($"Unable to convert \"{value}\" to ReadingContext")
        };
    }

    public override void Write(Utf8JsonWriter writer, ReadingContext? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        string stringValue = value.Value switch
        {
            ReadingContext.InterruptionBegin => "Interruption.Begin",
            ReadingContext.InterruptionEnd => "Interruption.End",
            ReadingContext.SampleClock => "Sample.Clock",
            ReadingContext.SamplePeriodic => "Sample.Periodic",
            ReadingContext.TransactionBegin => "Transaction.Begin",
            ReadingContext.TransactionEnd => "Transaction.End",
            ReadingContext.Trigger => "Trigger",
            ReadingContext.Other => "Other",
            _ => throw new JsonException($"Unknown ReadingContext value: {value}")
        };
        writer.WriteStringValue(stringValue);
    }
}

/// <summary>
/// Enumeration of formats for sampled values
/// </summary>
public enum ValueFormat
{
    Raw,
    SignedData
}

/// <summary>
/// Custom JSON converter for ValueFormat enum
/// </summary>
public class ValueFormatJsonConverter : JsonConverter<ValueFormat?>
{
    public override ValueFormat? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        string value = reader.GetString();
        return value switch
        {
            "Raw" => ValueFormat.Raw,
            "SignedData" => ValueFormat.SignedData,
            _ => throw new JsonException($"Unable to convert \"{value}\" to ValueFormat")
        };
    }

    public override void Write(Utf8JsonWriter writer, ValueFormat? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        string stringValue = value.Value switch
        {
            ValueFormat.Raw => "Raw",
            ValueFormat.SignedData => "SignedData",
            _ => throw new JsonException($"Unknown ValueFormat value: {value}")
        };
        writer.WriteStringValue(stringValue);
    }
}

/// <summary>
/// Enumeration of measurand types for sampled values
/// </summary>
public enum Measurand
{
    EnergyActiveExportRegister,
    EnergyActiveImportRegister,
    EnergyReactiveExportRegister,
    EnergyReactiveImportRegister,
    EnergyActiveExportInterval,
    EnergyActiveImportInterval,
    EnergyReactiveExportInterval,
    EnergyReactiveImportInterval,
    PowerActiveExport,
    PowerActiveImport,
    PowerOffered,
    PowerReactiveExport,
    PowerReactiveImport,
    PowerFactor,
    CurrentImport,
    CurrentExport,
    CurrentOffered,
    Voltage,
    Frequency,
    Temperature,
    SoC,
    RPM
}

/// <summary>
/// Custom JSON converter for Measurand enum to handle special formatting with periods
/// </summary>
public class MeasurandJsonConverter : JsonConverter<Measurand?>
{
    public override Measurand? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        string value = reader.GetString();
        return value switch
        {
            "Energy.Active.Export.Register" => Measurand.EnergyActiveExportRegister,
            "Energy.Active.Import.Register" => Measurand.EnergyActiveImportRegister,
            "Energy.Reactive.Export.Register" => Measurand.EnergyReactiveExportRegister,
            "Energy.Reactive.Import.Register" => Measurand.EnergyReactiveImportRegister,
            "Energy.Active.Export.Interval" => Measurand.EnergyActiveExportInterval,
            "Energy.Active.Import.Interval" => Measurand.EnergyActiveImportInterval,
            "Energy.Reactive.Export.Interval" => Measurand.EnergyReactiveExportInterval,
            "Energy.Reactive.Import.Interval" => Measurand.EnergyReactiveImportInterval,
            "Power.Active.Export" => Measurand.PowerActiveExport,
            "Power.Active.Import" => Measurand.PowerActiveImport,
            "Power.Offered" => Measurand.PowerOffered,
            "Power.Reactive.Export" => Measurand.PowerReactiveExport,
            "Power.Reactive.Import" => Measurand.PowerReactiveImport,
            "Power.Factor" => Measurand.PowerFactor,
            "Current.Import" => Measurand.CurrentImport,
            "Current.Export" => Measurand.CurrentExport,
            "Current.Offered" => Measurand.CurrentOffered,
            "Voltage" => Measurand.Voltage,
            "Frequency" => Measurand.Frequency,
            "Temperature" => Measurand.Temperature,
            "SoC" => Measurand.SoC,
            "RPM" => Measurand.RPM,
            _ => throw new JsonException($"Unable to convert \"{value}\" to Measurand")
        };
    }

    public override void Write(Utf8JsonWriter writer, Measurand? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        string stringValue = value.Value switch
        {
            Measurand.EnergyActiveExportRegister => "Energy.Active.Export.Register",
            Measurand.EnergyActiveImportRegister => "Energy.Active.Import.Register",
            Measurand.EnergyReactiveExportRegister => "Energy.Reactive.Export.Register",
            Measurand.EnergyReactiveImportRegister => "Energy.Reactive.Import.Register",
            Measurand.EnergyActiveExportInterval => "Energy.Active.Export.Interval",
            Measurand.EnergyActiveImportInterval => "Energy.Active.Import.Interval",
            Measurand.EnergyReactiveExportInterval => "Energy.Reactive.Export.Interval",
            Measurand.EnergyReactiveImportInterval => "Energy.Reactive.Import.Interval",
            Measurand.PowerActiveExport => "Power.Active.Export",
            Measurand.PowerActiveImport => "Power.Active.Import",
            Measurand.PowerOffered => "Power.Offered",
            Measurand.PowerReactiveExport => "Power.Reactive.Export",
            Measurand.PowerReactiveImport => "Power.Reactive.Import",
            Measurand.PowerFactor => "Power.Factor",
            Measurand.CurrentImport => "Current.Import",
            Measurand.CurrentExport => "Current.Export",
            Measurand.CurrentOffered => "Current.Offered",
            Measurand.Voltage => "Voltage",
            Measurand.Frequency => "Frequency",
            Measurand.Temperature => "Temperature",
            Measurand.SoC => "SoC",
            Measurand.RPM => "RPM",
            _ => throw new JsonException($"Unknown Measurand value: {value}")
        };
        writer.WriteStringValue(stringValue);
    }
}

/// <summary>
/// Enumeration of phase types for sampled values
/// </summary>
public enum Phase
{
    L1,
    L2,
    L3,
    N,
    L1N,
    L2N,
    L3N,
    L1L2,
    L2L3,
    L3L1
}

/// <summary>
/// Custom JSON converter for Phase enum to handle special formatting with hyphens
/// </summary>
public class PhaseJsonConverter : JsonConverter<Phase?>
{
    public override Phase? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        string value = reader.GetString();
        return value switch
        {
            "L1" => Phase.L1,
            "L2" => Phase.L2,
            "L3" => Phase.L3,
            "N" => Phase.N,
            "L1-N" => Phase.L1N,
            "L2-N" => Phase.L2N,
            "L3-N" => Phase.L3N,
            "L1-L2" => Phase.L1L2,
            "L2-L3" => Phase.L2L3,
            "L3-L1" => Phase.L3L1,
            _ => throw new JsonException($"Unable to convert \"{value}\" to Phase")
        };
    }

    public override void Write(Utf8JsonWriter writer, Phase? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        string stringValue = value.Value switch
        {
            Phase.L1 => "L1",
            Phase.L2 => "L2",
            Phase.L3 => "L3",
            Phase.N => "N",
            Phase.L1N => "L1-N",
            Phase.L2N => "L2-N",
            Phase.L3N => "L3-N",
            Phase.L1L2 => "L1-L2",
            Phase.L2L3 => "L2-L3",
            Phase.L3L1 => "L3-L1",
            _ => throw new JsonException($"Unknown Phase value: {value}")
        };
        writer.WriteStringValue(stringValue);
    }
}

/// <summary>
/// Enumeration of location types for sampled values
/// </summary>
public enum Location
{
    Cable,
    EV,
    Inlet,
    Outlet,
    Body
}

/// <summary>
/// Custom JSON converter for Location enum
/// </summary>
public class LocationJsonConverter : JsonConverter<Location?>
{
    public override Location? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        string value = reader.GetString();
        return value switch
        {
            "Cable" => Location.Cable,
            "EV" => Location.EV,
            "Inlet" => Location.Inlet,
            "Outlet" => Location.Outlet,
            "Body" => Location.Body,
            _ => throw new JsonException($"Unable to convert \"{value}\" to Location")
        };
    }

    public override void Write(Utf8JsonWriter writer, Location? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        string stringValue = value.Value switch
        {
            Location.Cable => "Cable",
            Location.EV => "EV",
            Location.Inlet => "Inlet",
            Location.Outlet => "Outlet",
            Location.Body => "Body",
            _ => throw new JsonException($"Unknown Location value: {value}")
        };
        writer.WriteStringValue(stringValue);
    }
}

/// <summary>
/// Enumeration of units of measurement for sampled values
/// </summary>
public enum UnitOfMeasure
{
    Wh,
    KWh,
    Varh,
    Kvarh,
    W,
    KW,
    VA,
    KVA,
    Var,
    Kvar,
    A,
    V,
    K,
    Celcius,
    Celsius,
    Fahrenheit,
    Percent,
    Hertz
}

/// <summary>
/// Custom JSON converter for UnitOfMeasure enum
/// </summary>
public class UnitOfMeasureJsonConverter : JsonConverter<UnitOfMeasure?>
{
    public override UnitOfMeasure? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        string value = reader.GetString();
        return value switch
        {
            "Wh" => UnitOfMeasure.Wh,
            "kWh" => UnitOfMeasure.KWh,
            "varh" => UnitOfMeasure.Varh,
            "kvarh" => UnitOfMeasure.Kvarh,
            "W" => UnitOfMeasure.W,
            "kW" => UnitOfMeasure.KW,
            "VA" => UnitOfMeasure.VA,
            "kVA" => UnitOfMeasure.KVA,
            "var" => UnitOfMeasure.Var,
            "kvar" => UnitOfMeasure.Kvar,
            "A" => UnitOfMeasure.A,
            "V" => UnitOfMeasure.V,
            "K" => UnitOfMeasure.K,
            "Celcius" => UnitOfMeasure.Celcius,
            "Celsius" => UnitOfMeasure.Celsius,
            "Fahrenheit" => UnitOfMeasure.Fahrenheit,
            "Percent" => UnitOfMeasure.Percent,
            "Hertz" => UnitOfMeasure.Hertz,
            _ => throw new JsonException($"Unable to convert \"{value}\" to UnitOfMeasure")
        };
    }

    public override void Write(Utf8JsonWriter writer, UnitOfMeasure? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        string stringValue = value.Value switch
        {
            UnitOfMeasure.Wh => "Wh",
            UnitOfMeasure.KWh => "kWh",
            UnitOfMeasure.Varh => "varh",
            UnitOfMeasure.Kvarh => "kvarh",
            UnitOfMeasure.W => "W",
            UnitOfMeasure.KW => "kW",
            UnitOfMeasure.VA => "VA",
            UnitOfMeasure.KVA => "kVA",
            UnitOfMeasure.Var => "var",
            UnitOfMeasure.Kvar => "kvar",
            UnitOfMeasure.A => "A",
            UnitOfMeasure.V => "V",
            UnitOfMeasure.K => "K",
            UnitOfMeasure.Celcius => "Celcius",
            UnitOfMeasure.Celsius => "Celsius",
            UnitOfMeasure.Fahrenheit => "Fahrenheit",
            UnitOfMeasure.Percent => "Percent",
            UnitOfMeasure.Hertz => "Hertz",
            _ => throw new JsonException($"Unknown UnitOfMeasure value: {value}")
        };
        writer.WriteStringValue(stringValue);
    }
}
