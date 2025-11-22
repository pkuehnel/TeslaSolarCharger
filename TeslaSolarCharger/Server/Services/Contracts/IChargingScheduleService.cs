using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IChargingScheduleService
{
    Task<List<DtoChargingSchedule>> GenerateChargingSchedulesForLoadPoint(DtoLoadPointOverview loadpoint,
        List<DtoTimeZonedChargingTarget> loadPointRelevantChargingTargets, Dictionary<DateTimeOffset, int> predictedSurplusSlices,
        DateTimeOffset currentDate,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adds +1 to the target SOC if the car side SOC limit is equal to the charging target SOC to force the car to stop charging by itself.
    /// </summary>
    int? GetActualTargetSoc(int? carSideSocLimit, int? chargingTargetTargetSoc, bool isCurrentlyCharging);
}
