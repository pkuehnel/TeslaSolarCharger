using Microsoft.EntityFrameworkCore;
using PkSoftwareService.Custom.Backend;
using Serilog.Events;
using System.Text;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Ocpp;
using TeslaSolarCharger.Server.Services.ChargepointAction;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Support;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class DebugService(ILogger<DebugService> logger,
    ITeslaSolarChargerContext context,
    IInMemorySink inMemorySink,
    Serilog.Core.LoggingLevelSwitch inMemoryLogLevelSwitch,
    IOcppChargePointActionService ocppChargePointActionService,
    IConstants constants,
    ISettings settings) : IDebugService
{
    public async Task<Dictionary<int, DtoDebugChargingConnector>> GetChargingConnectors()
    {
        logger.LogTrace("{method}()", nameof(GetChargingConnectors));
        var connectors = await context.OcppChargingStationConnectors
            .Include(x => x.OcppChargingStation)
            .ToDictionaryAsync(x => x.Id, x => new DtoDebugChargingConnector(x.OcppChargingStation.ChargepointId, x.Name)
            {
                ConnectorId = x.ConnectorId,
            }).ConfigureAwait(false);
        logger.LogDebug("Found {connectorCount} connectors", connectors.Count);
        foreach (var connector in connectors)
        {
            connector.Value.ConnectorState = settings.OcppConnectorStates.TryGetValue(connector.Key, out var state) ? state : null;
        }
        return connectors;
    }

    public async Task<Result<RemoteStartTransactionResponse?>> StartCharging(string chargePointId, int connectorId, decimal currentToSet, int? numberOfPhases,
        CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({chargePointId}, {connectorId}, {currentToSet}, {numberOfPhases})", nameof(StartCharging),
            chargePointId, connectorId, currentToSet, numberOfPhases);
        
        var result = await ocppChargePointActionService.StartCharging(
            chargePointId + constants.OcppChargePointConnectorIdDelimiter + connectorId,
            currentToSet,
            numberOfPhases,
            cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<Result<RemoteStopTransactionResponse?>> StopCharging(string chargePointId, int connectorId, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({chargePointId}, {connectorId})", nameof(StopCharging), chargePointId, connectorId);
        var result = await ocppChargePointActionService.StopCharging(
            chargePointId + constants.OcppChargePointConnectorIdDelimiter + connectorId, cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<Result<SetChargingProfileResponse?>> SetCurrentAndPhases(string chargePointId, int connectorId, decimal currentToSet, int? numberOfPhases,
        CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({chargePointId}, {connectorId}, {currentToSet}, {numberOfPhases})", nameof(SetCurrentAndPhases),
            chargePointId, connectorId, currentToSet, numberOfPhases);

        var result = await ocppChargePointActionService.SetChargingCurrent(
            chargePointId + constants.OcppChargePointConnectorIdDelimiter + connectorId,
            currentToSet,
            numberOfPhases,
            cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<Dictionary<int, DtoDebugCar>> GetCars()
    {
        logger.LogTrace("{method}", nameof(GetCars));
        var cars = await context.Cars
            .Where(x => x.Vin != null)
            .ToDictionaryAsync(x => x.Id, x => new DtoDebugCar()
            {
                Name = x.Name,
                Vin = x.Vin,
                ShouldBeManaged = x.ShouldBeManaged == true,
                IsAvailableInTeslaAccount = x.IsAvailableInTeslaAccount,
            }).ConfigureAwait(false);
        logger.LogDebug("Found {carCount} cars", cars.Count);
        return cars;
    }

    public byte[] GetLogBytes()
    {
        logger.LogTrace("{method}", nameof(GetLogBytes));
        var logEntries = inMemorySink.GetLogs();
        var content = string.Join(Environment.NewLine, logEntries);
        var bytes = Encoding.UTF8.GetBytes(content);
        return bytes;
    }

    public string GetLogLevel()
    {
        logger.LogTrace("{method}", nameof(GetLogLevel));
        return inMemoryLogLevelSwitch.MinimumLevel.ToString();
    }

    public void SetLogLevel(string level)
    {
        logger.LogTrace("{method} {level}", nameof(SetLogLevel), level);
        if (!Enum.TryParse<LogEventLevel>(level, true, out var newLevel))
        {
            throw new ArgumentException("Invalid log level. Use one of: Verbose, Debug, Information, Warning, Error, Fatal", nameof(level));
        }
        inMemoryLogLevelSwitch.MinimumLevel = newLevel;
    }

    public int GetLogCapacity()
    {
        logger.LogTrace("{method}", nameof(GetLogCapacity));
        return inMemorySink.GetCapacity();
    }

    public void SetLogCapacity(int capacity)
    {
        logger.LogTrace("{method} {capacity}", nameof(SetLogCapacity), capacity);
        inMemorySink.UpdateCapacity(capacity);
    }
}
