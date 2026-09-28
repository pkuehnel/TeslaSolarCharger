namespace TeslaSolarCharger.Shared.Dtos.Settings;

/// <summary>
/// Until when (UTC) Fleet API requests of a car are blocked by the Solar4Car backend's command budget. Null means not blocked.
/// </summary>
public record DtoFleetApiBudgetBlocks(DateTime? CommandsBlockedUntil = null, DateTime? WakeUpBlockedUntil = null,
    DateTime? FleetApiTestBlockedUntil = null);
