using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Solar4CarBackend;
using TeslaSolarCharger.Server.Enums;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services;

/// <summary>
/// Keeps Fleet API requests within the Solar4Car backend's command budget, so the backend never has to reject one. The
/// budget is only evaluated by the backend; this service asks for it before a rate limited request and remembers blocks,
/// so a blocked car does not cause a backend request per charging cycle.
/// </summary>
public class FleetApiRateLimitService(
    ILogger<FleetApiRateLimitService> logger,
    IDateTimeProvider dateTimeProvider,
    IBackendApiService backendApiService) : IFleetApiRateLimitService
{
    /// <summary>
    /// Subtracted from grace windows and added to blocks, so the time between asking for the budget and the request reaching
    /// the backend can never let a request run into a limit.
    /// </summary>
    public static readonly TimeSpan SafetyMargin = TimeSpan.FromSeconds(30);

    public async Task<Result<DtoFleetApiBudgetBlocks>> GetBlocks(DtoCar car, IReadOnlyCollection<FleetApiBudgetKind> kinds)
    {
        logger.LogTrace("{method}({vin}, {@kinds})", nameof(GetBlocks), car.Vin, kinds);
        var currentDate = dateTimeProvider.UtcNow();
        var knownBlocks = RunningBlocks(car.FleetApiBudgetBlocks, currentDate);
        if (kinds.Any(kind => BlockedUntil(knownBlocks, kind) != null))
        {
            return new(knownBlocks, null, null);
        }
        var budget = await backendApiService.GetFleetApiCommandBudget(car.Vin).ConfigureAwait(false);
        if (budget.HasError || budget.Data == default)
        {
            logger.LogError("Could not get the Fleet API command budget of car {vin}: {errorMessage}", car.Vin, budget.ErrorMessage);
            return new(null, budget.ErrorMessage ?? "The Solar4Car backend did not return a command budget.", budget.ProblemDetails);
        }
        car.FleetApiBudgetBlocks = ToBlocks(budget.Data, currentDate);
        return new(RunningBlocks(car.FleetApiBudgetBlocks, currentDate), null, null);
    }

    public bool IsKnownToBeBlocked(DtoCar car, FleetApiBudgetKind kind) =>
        BlockedUntil(RunningBlocks(car.FleetApiBudgetBlocks, dateTimeProvider.UtcNow()), kind) != null;

    internal static DtoFleetApiBudgetBlocks ToBlocks(DtoFleetApiCommandBudget budget, DateTime currentDate)
    {
        var isWithinGraceWindow = budget.GraceRemainingSeconds is { } graceRemainingSeconds
                                  && TimeSpan.FromSeconds(graceRemainingSeconds) > SafetyMargin;
        return new(
            isWithinGraceWindow ? null : BlockEnd(budget.NextCommandInSeconds, currentDate),
            BlockEnd(budget.NextWakeUpInSeconds, currentDate),
            BlockEnd(budget.NextFleetApiTestInSeconds, currentDate));
    }

    private static DateTime? BlockEnd(int? seconds, DateTime currentDate) =>
        seconds == null ? null : currentDate + TimeSpan.FromSeconds(seconds.Value) + SafetyMargin;

    private static DtoFleetApiBudgetBlocks RunningBlocks(DtoFleetApiBudgetBlocks blocks, DateTime currentDate) =>
        new(Running(blocks.CommandsBlockedUntil, currentDate),
            Running(blocks.WakeUpBlockedUntil, currentDate),
            Running(blocks.FleetApiTestBlockedUntil, currentDate));

    private static DateTime? Running(DateTime? blockedUntil, DateTime currentDate) =>
        blockedUntil > currentDate ? blockedUntil : null;

    internal static DateTime? BlockedUntil(DtoFleetApiBudgetBlocks blocks, FleetApiBudgetKind kind) => kind switch
    {
        FleetApiBudgetKind.Commands => blocks.CommandsBlockedUntil,
        FleetApiBudgetKind.WakeUp => blocks.WakeUpBlockedUntil,
        FleetApiBudgetKind.FleetApiTest => blocks.FleetApiTestBlockedUntil,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
