using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Server.Services.HomeBatteryControl.Contracts;

public interface IHomeBatteryScheduleService
{
    /// <summary>
    /// Plans home battery hold/charge windows based on grid prices, the predicted energy deficit until solar self
    /// sufficiency and the given car charging schedules. The result is stored in
    /// <see cref="Shared.Dtos.Contracts.ISettings.HomeBatteryScheduleWindows"/>.
    /// </summary>
    Task PlanScheduleWindows(DateTimeOffset currentDate, List<DtoChargingSchedule> chargingSchedules, CancellationToken cancellationToken);
}
