using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IShouldStartStopChargingCalculator
{
    void SetStartStopChargingForLoadPoint(DtoLoadPointOverview dtoLoadPointOverview, int targetPower, List<DtoStartStopChargingHelper> carElements, List<DtoStartStopChargingHelper> ocppElements, DateTimeOffset currentDate);
    Task<List<DtoStartStopChargingHelper>> GetCarElements();
    Task<List<DtoStartStopChargingHelper>> GetOcppElements();
}
