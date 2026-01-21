using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;

namespace TeslaSolarCharger.Server.Services.Contracts;

using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public interface IChargingServiceV2
{
    Task SetNewChargingValues(CancellationToken cancellationToken);
    DateTimeOffset? GetNextTargetUtc(CarChargingTarget chargingTarget, DateTimeOffset lastPluggedIn);
}
