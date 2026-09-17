using TeslaSolarCharger.Shared.Dtos.Settings;
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

    /// <summary>The value together with when the device last delivered it.</summary>
    public DtoTimeStampedValue<decimal> Value { get; set; } = new(DateTimeOffset.MinValue, 0);
}
