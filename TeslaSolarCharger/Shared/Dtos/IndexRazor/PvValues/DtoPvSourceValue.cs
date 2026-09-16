using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;

/// <summary>
/// What one device currently contributes to one measurement. A device that reads a measurement from several places,
/// like a hybrid inverter with separate charge and discharge registers, is already added up into one entry.
/// </summary>
public class DtoPvSourceValue
{
    public ConfigurationType ConfigurationType { get; set; }

    /// <summary>The id of the configuration the value comes from, within its <see cref="ConfigurationType"/>.</summary>
    public int SourceId { get; set; }

    public ValueUsage UsedFor { get; set; }

    public decimal Value { get; set; }

    /// <summary>When the device last delivered this value.</summary>
    public DateTimeOffset LastUpdated { get; set; }
}
