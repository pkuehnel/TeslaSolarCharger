﻿using TeslaSolarCharger.Shared.Dtos.ChargingStation;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IOcppChargingStationConfigurationService
{
    Task AddChargingStationIfNotExisting(string chargepointId, CancellationToken httpContextRequestAborted);
    Task<List<DtoChargingStation>> GetChargingStations();
    Task UpdateChargingStationConnector(DtoChargingStationConnector dtoChargingStation);
    Task<List<DtoChargingStationConnector>> GetChargingStationConnectors(int chargingStationId);
}
