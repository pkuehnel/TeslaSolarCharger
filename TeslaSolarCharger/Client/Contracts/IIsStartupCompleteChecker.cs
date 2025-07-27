namespace TeslaSolarCharger.Client.Contracts;

public interface IIsStartupCompleteChecker
{
    /// <summary>
    /// Checks if the startup is complete.
    /// </summary>
    /// <returns>True if the startup is complete, otherwise false.</returns>
    Task<bool> IsStartupCompleteAsync();
}
