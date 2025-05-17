using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Ocpp;

namespace TeslaSolarCharger.Server.Services.ChargepointAction;

public interface IChargePointActionService
{
    Task<Result<RemoteStartTransactionResponse?>> StartCharging(string chargepointIdentifier, decimal currentToSet, int? numberOfPhases,
        CancellationToken cancellationToken);
    Task<Result<object?>> StopCharging(string chargepointIdentifier, CancellationToken cancellationToken);
    Task<Result<object?>> SetChargingCurrent(string chargepointIdentifier, decimal currentToSet, int? numberOfPhases,
        CancellationToken cancellationToken);
}
