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
}
