using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services;

/// <summary>
/// Rate limits Fleet API commands for BLE enabled cars without a Fleet API license: one counted successful command per
/// <see cref="CommandWindow"/>. The first counted command opens a <see cref="GraceWindow"/> during which further commands
/// are allowed without consuming the budget, so multi command sequences like wake up, set amps, charge start can complete.
/// The same limits are enforced in the Solar4Car backend, so these values must not be changed without changing them there, too.
/// </summary>
public class FleetApiRateLimitService(
    ILogger<FleetApiRateLimitService> logger,
    IDateTimeProvider dateTimeProvider) : IFleetApiRateLimitService
{
    public static readonly TimeSpan CommandWindow = TimeSpan.FromMinutes(60);
    public static readonly TimeSpan GraceWindow = TimeSpan.FromMinutes(5);

    public DateTime? GetNextAllowedUtc(DtoCar car)
    {
        logger.LogTrace("{method}({vin})", nameof(GetNextAllowedUtc), car.Vin);
        var lastCountedCommand = car.LastCountedFleetApiCommand;
        if (lastCountedCommand == default)
        {
            return null;
        }
        var currentDate = dateTimeProvider.UtcNow();
        if (currentDate < (lastCountedCommand.Value + GraceWindow))
        {
            return null;
        }
        if (currentDate >= (lastCountedCommand.Value + CommandWindow))
        {
            return null;
        }
        return lastCountedCommand.Value + CommandWindow;
    }

    public void RecordSuccessfulCommand(DtoCar car)
    {
        logger.LogTrace("{method}({vin})", nameof(RecordSuccessfulCommand), car.Vin);
        var lastCountedCommand = car.LastCountedFleetApiCommand;
        var currentDate = dateTimeProvider.UtcNow();
        if (lastCountedCommand == default || currentDate >= (lastCountedCommand.Value + CommandWindow))
        {
            car.LastCountedFleetApiCommand = currentDate;
        }
    }

    /// <summary>
    /// Blocks further commands after the backend rejected one as rate limited. As <see cref="DtoCar.LastCountedFleetApiCommand"/>
    /// is only kept in memory but the backend enforces the limit across restarts, the local state can be empty while the backend
    /// still blocks. Without this every charging cycle would send a command just to get rejected again.
    /// The block is anchored a <see cref="GraceWindow"/> in the past so no new grace window is opened. As the backend does not
    /// tell when exactly the next command is allowed, this blocks for the remaining <see cref="CommandWindow"/> minus
    /// <see cref="GraceWindow"/>, which never blocks longer than a full command window.
    /// </summary>
    public void RecordRateLimited(DtoCar car)
    {
        logger.LogTrace("{method}({vin})", nameof(RecordRateLimited), car.Vin);
        var lastCountedCommand = car.LastCountedFleetApiCommand;
        var blockAnchor = dateTimeProvider.UtcNow() - GraceWindow;
        //Never shorten an already known block, e.g. when the backend rejects a command sent within the local grace window.
        if (lastCountedCommand == default || blockAnchor > lastCountedCommand.Value)
        {
            car.LastCountedFleetApiCommand = blockAnchor;
        }
    }
}
