using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Ocpp;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Dtos.Support;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IDebugService
{
    Task<Dictionary<int, DtoDebugCar>> GetCars();
    byte[] GetLogBytes();
    void SetLogLevel(string level);
    void SetLogCapacity(int capacity);
    string GetLogLevel();
    int GetLogCapacity();
    Task<Dictionary<int, DtoDebugChargingConnector>> GetChargingConnectors();

    Task<Result<RemoteStartTransactionResponse?>> StartCharging(string chargePointId, int connectorId, decimal currentToSet, int? numberOfPhases,
        CancellationToken cancellationToken);

    Task<Result<RemoteStopTransactionResponse?>> StopCharging(string chargePointId, int connectorId, CancellationToken cancellationToken);

    Task<Result<SetChargingProfileResponse?>> SetCurrentAndPhases(string chargePointId, int connectorId, decimal currentToSet, int? numberOfPhases,
        CancellationToken cancellationToken);

    DtoOcppConnectorState GetOcppConnectorState(int connectorId);
}
