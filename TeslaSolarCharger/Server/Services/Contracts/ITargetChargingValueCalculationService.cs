using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ITargetChargingValueCalculationService
{
    Task AppendTargetValues(List<DtoTargetChargingValues> targetChargingValues,
        List<DtoChargingSchedule> activeChargingSchedules, DateTimeOffset currentDate, int powerToControl,
        CancellationToken cancellationToken);
}
