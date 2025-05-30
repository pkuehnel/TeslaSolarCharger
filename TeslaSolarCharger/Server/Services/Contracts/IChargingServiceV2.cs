using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IChargingServiceV2
{
    Task SetNewChargingValues(int? restPowerToUse, CancellationToken cancellationToken);
}
