using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

namespace TeslaSolarCharger.Server.Services;

public interface IMeterValueEstimationService
{
    Task FillMissingEstimatedMeterValuesInDatabase();

    /// <summary>
    /// Update the meter value estimations based on the latest known value.
    /// </summary>
    /// <param name="meterValue">Meter values whose values should be estimated</param>
    /// <param name="latestKnownValue">latest known value of the same kind like meter value. Calling with null is not recommended as this results in a call to the database and dramatically slows down method execution time</param>
    /// <returns>New latest known value that can be used for future method calls as baseline</returns>
    /// <exception cref="InvalidDataException">Thrown when latest known value and meter value do not have the same kind.</exception>
    Task<MeterValue> UpdateMeterValueEstimation(MeterValue meterValue, MeterValue? latestKnownValue);
}
