﻿using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Ocpp;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Dtos.Support;
using MeterValue = TeslaSolarCharger.Model.Entities.TeslaSolarCharger.MeterValue;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IDebugService
{
    Task<Dictionary<int, DtoDebugCar>> GetCars();
    void SetInMemoryLogLevel(string level);
    void SetLogCapacity(int capacity);
    string GetInMemoryLogLevel();
    int GetLogCapacity();
    Task<Dictionary<int, DtoDebugChargingConnector>> GetChargingConnectors();

    Task<Result<RemoteStartTransactionResponse?>> StartCharging(string chargePointId, int connectorId, decimal currentToSet, int? numberOfPhases,
        CancellationToken cancellationToken);

    Task<Result<RemoteStopTransactionResponse?>> StopCharging(string chargePointId, int connectorId, CancellationToken cancellationToken);

    Task<Result<SetChargingProfileResponse?>> SetCurrentAndPhases(string chargePointId, int connectorId, decimal currentToSet, int? numberOfPhases,
        CancellationToken cancellationToken);

    DtoOcppConnectorState GetOcppConnectorState(int connectorId);
    DtoCar? GetDtoCar(int carId);
    void SetFileLogLevel(string level);
    string GetFileLogLevel();
    Task WriteFileLogsToStream(Stream outputStream);
    Task StreamLogsToAsync(Stream stream);
    Task<List<MeterValue>> GetLatestMeterValues();
    List<string> GetLogs(int? tail);
}
