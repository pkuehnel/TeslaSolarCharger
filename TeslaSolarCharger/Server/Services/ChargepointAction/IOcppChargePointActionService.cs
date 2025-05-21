using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Ocpp;

namespace TeslaSolarCharger.Server.Services.ChargepointAction;

public interface IOcppChargePointActionService
{
    Task<Result<RemoteStartTransactionResponse?>> StartCharging(string chargepointIdentifier, decimal currentToSet, int? numberOfPhases,
        CancellationToken cancellationToken);
    Task<Result<RemoteStopTransactionResponse?>> StopCharging(string chargepointIdentifier, CancellationToken cancellationToken);
    Task<Result<SetChargingProfileResponse?>> SetChargingCurrent(string chargepointIdentifier, decimal currentToSet, int? numberOfPhases,
        CancellationToken cancellationToken);

    Task<Result<RemoteStartTransactionResponse?>> StartCharging(int chargingConnectorId, decimal currentToSet,
        int? numberOfPhases,
        CancellationToken cancellationToken);

    Task<Result<RemoteStopTransactionResponse?>> StopCharging(int chargingConnectorId, CancellationToken cancellationToken);

    Task<Result<SetChargingProfileResponse?>> SetChargingCurrent(int chargingConnectorId, decimal currentToSet,
        int? numberOfPhases,
        CancellationToken cancellationToken);
}
