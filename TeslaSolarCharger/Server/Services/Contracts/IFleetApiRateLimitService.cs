using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Enums;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IFleetApiRateLimitService
{
    /// <summary>
    /// Until when the given parts of the command budget block the car. Asks the Solar4Car backend unless one of them is
    /// already known to block, as the request is refused then anyway. Blocks that are over are returned as null.
    /// </summary>
    Task<Result<DtoFleetApiBudgetBlocks>> GetBlocks(DtoCar car, IReadOnlyCollection<FleetApiBudgetKind> kinds);
    /// <summary>
    /// Whether the last known budget still blocks the car. Never asks the backend.
    /// </summary>
    bool IsKnownToBeBlocked(DtoCar car, FleetApiBudgetKind kind);
}
