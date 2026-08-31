namespace TeslaSolarCharger.Server.Dtos.HomeBatteryControl;

/// <summary>
/// Predicted energy surplus data used to plan home battery SoC targets and hold/charge windows.
/// </summary>
public class DtoHomeBatterySurplusPrediction
{
    public DtoHomeBatterySurplusPrediction(Dictionary<DateTimeOffset, int> surplusPerSlice, DateTimeOffset selfSufficiencyTime, bool isTargetDateSunrise)
    {
        SurplusPerSlice = surplusPerSlice;
        SelfSufficiencyTime = selfSufficiencyTime;
        IsTargetDateSunrise = isTargetDateSunrise;
    }

    /// <summary>
    /// Predicted solar production minus house consumption in Wh per slice, keyed by slice start.
    /// </summary>
    public Dictionary<DateTimeOffset, int> SurplusPerSlice { get; }

    /// <summary>
    /// The time the battery needs to last until: the next sunrise adjusted to the first positive surplus, or the next
    /// sunset when the battery should be full by sunset.
    /// </summary>
    public DateTimeOffset SelfSufficiencyTime { get; }

    public bool IsTargetDateSunrise { get; }
}
