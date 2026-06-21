using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos;

public class DtoCarDeletionProgress
{
    /// <summary>
    /// Number of steps already completed.
    /// </summary>
    public int Value { get; set; }

    /// <summary>
    /// Total number of steps the deletion consists of.
    /// </summary>
    public int MaxValue { get; set; }

    /// <summary>
    /// The data that is currently being deleted.
    /// </summary>
    public CarDeletionStep CurrentStep { get; set; }
}
