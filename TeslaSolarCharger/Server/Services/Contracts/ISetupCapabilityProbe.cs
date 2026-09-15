using TeslaSolarCharger.Shared.Dtos.Setup;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ISetupCapabilityProbe
{
    /// <summary>
    /// Detects what the installation can currently do. Never throws for a single unavailable check: an account or
    /// measurement that cannot be reached is reported as unknown so setup can still explain the rest.
    /// </summary>
    Task<DtoSetupCapabilities> GetCapabilities();

    /// <summary>
    /// What one car turned out to be able to do. Null when the car does not exist, which is the normal answer for a
    /// draft that has not been saved yet.
    /// </summary>
    Task<DtoSetupCarCapabilities?> GetCarCapabilities(int carId);
}
