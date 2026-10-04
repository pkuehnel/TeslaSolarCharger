namespace TeslaSolarCharger.Server.Dtos.Solar4CarBackend;

/// <summary>
/// What the Solar4Car backend currently allows to be sent to a car via Fleet API. All values are relative to the time of
/// the answer, so a clock difference between TSC and the backend does not matter. Null means not limited.
/// </summary>
public class DtoFleetApiCommandBudget
{
    /// <summary>
    /// Remaining grace window of the last counted command of a car without Fleet API license. Rounded down.
    /// </summary>
    public int? GraceRemainingSeconds { get; set; }
    /// <summary>
    /// Time until the hourly command budget of a car without Fleet API license is available again. Rounded up. Commands
    /// are still allowed while <see cref="GraceRemainingSeconds"/> is set.
    /// </summary>
    public int? NextCommandInSeconds { get; set; }
    /// <summary>
    /// Time until the next wake up is allowed. Applies to all cars. Rounded up.
    /// </summary>
    public int? NextWakeUpInSeconds { get; set; }
    /// <summary>
    /// Time until the next Fleet API access test of a car without Fleet API license is allowed. Rounded up.
    /// </summary>
    public int? NextFleetApiTestInSeconds { get; set; }
}
