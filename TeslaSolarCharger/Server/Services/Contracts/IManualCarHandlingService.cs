namespace TeslaSolarCharger.Server.Services.Contracts;

using System;
using System.Threading.Tasks;
using TeslaSolarCharger.Shared.Dtos.Settings;

public interface IManualCarHandlingService
{
    Task UpdateStateOfChargeAsync(int carId, int newStateOfCharge);

    Task<ManualCarOperationResult> UpdateStateFromConnectorAsync(int carId, DtoOcppConnectorState connectorState);

    Task<ManualCarOperationResult> HandleConnectorAssignmentAsync(int carId, bool? isCharging, DateTimeOffset timestamp);

    Task<ManualCarOperationResult> HandleConnectorUnassignmentAsync(int carId, DateTimeOffset timestamp);
}
