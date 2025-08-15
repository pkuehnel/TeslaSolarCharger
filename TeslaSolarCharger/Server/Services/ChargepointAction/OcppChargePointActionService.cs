﻿using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Ocpp;
using TeslaSolarCharger.Server.Dtos.Ocpp.Generics;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services.ChargepointAction;

public class OcppChargePointActionService(ILogger<OcppChargePointActionService> logger,
    IConstants constants,
    IOcppWebSocketConnectionHandlingService ocppWebSocketConnectionHandlingService,
    ITeslaSolarChargerContext context,
    IDateTimeProvider dateTimeProvider,
    ISettings settings) : IOcppChargePointActionService
{
    public async Task<Result<RemoteStartTransactionResponse?>> StartCharging(int chargingConnectorId, decimal currentToSet,
        int? numberOfPhases,
        CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({chargingConnectorId}, {currentToSet}, {numberOfPhases})", nameof(StartCharging), chargingConnectorId, currentToSet, numberOfPhases);
        var chargePointIdentifier = await GetChargePointIdentifierByChargingConnectorId(chargingConnectorId, cancellationToken).ConfigureAwait(false);
        return await StartCharging(chargePointIdentifier, currentToSet, numberOfPhases, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RemoteStartTransactionResponse?>> StartCharging(string chargepointIdentifier, decimal currentToSet, int? numberOfPhases,
        CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({chargePointIdentifier}, {currentToSet}, {numberOfPhases})", nameof(StartCharging), chargepointIdentifier, currentToSet, numberOfPhases);
        string chargePointId;
        int connectorId;
        try
        {
            chargePointId = GetChargePointAndConnectorId(chargepointIdentifier, out connectorId);
        }
        catch (ArgumentException ex)
        {
            return new(null, ex.Message, null);
        }
        var openTransactions = await context.OcppTransactions
            .Where(t => t.ChargingStationConnector.OcppChargingStation.ChargepointId == chargePointId
                        && t.ChargingStationConnector.ConnectorId == connectorId
                        && t.EndDate == null)
            .OrderBy(t => t.StartDate)
            .ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (openTransactions.Any())
        {
            var result = await SetChargingCurrent(chargepointIdentifier, currentToSet, numberOfPhases, cancellationToken);
            if (!result.HasError)
            {
                return new Result<RemoteStartTransactionResponse?>(
                    new RemoteStartTransactionResponse()
                    {
                        Status = RemoteStartStopStatus.Accepted,
                    }, null, null);
            }
        }
        foreach (var openTransaction in openTransactions)
        {
            if (openTransaction != openTransactions.Last())
            {
                openTransaction.EndDate = dateTimeProvider.DateTimeOffSetUtcNow();
            }
        }
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var remoteStartTransaction = new RemoteStartTransactionRequest()
        {
            ConnectorId = connectorId,
            IdTag = constants.DefaultIdTag,
            ChargingProfile = GenerateChargingProfile(true, currentToSet, numberOfPhases),
        };
        try
        {
            var ocppResponse = await ocppWebSocketConnectionHandlingService.SendRequestAsync<RemoteStartTransactionResponse>(chargePointId,
                "RemoteStartTransaction",
                remoteStartTransaction,
                cancellationToken);
            if (ocppResponse.Status != RemoteStartStopStatus.Accepted)
            {
                logger.LogError("Error while sending RemoteStartTransaction to charge point {chargePointId}: Not Accepted", chargePointId);
                return new(ocppResponse, $"The Charge point {chargePointId} did not accept the request", null);
            }
            await UpdateLastSetValues(chargePointId, connectorId, currentToSet, numberOfPhases, true, cancellationToken).ConfigureAwait(false);
            return new(ocppResponse, null, null);
        }
        catch (OcppCallErrorException ex)
        {
            logger.LogError(ex, "Error while sending RemoteStartTransaction to charge point {chargePointId}", chargePointId);
            return new(null, ex.Code + " " + ex.Description, null);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogError(ex, "Timeout while sending RemoteStartTransaction to charge point {chargePointId}", chargePointId);
            return new(null, ex.Message, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not send message to charge point {chargePointId} or charge point did not answer properly", chargePointId);
            return new(null, ex.Message, null);
        }

    }

    public async Task<Result<RemoteStopTransactionResponse?>> StopCharging(int chargingConnectorId, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({chargingConnectorId})", nameof(StopCharging), chargingConnectorId);
        var chargePointIdentifier = await GetChargePointIdentifierByChargingConnectorId(chargingConnectorId, cancellationToken).ConfigureAwait(false);
        return await StopCharging(chargePointIdentifier, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RemoteStopTransactionResponse?>> StopCharging(string chargepointIdentifier, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({chargePointIdentifier})", nameof(StopCharging), chargepointIdentifier);
        string chargePointId;
        int connectorId;
        try
        {
            chargePointId = GetChargePointAndConnectorId(chargepointIdentifier, out connectorId);
        }
        catch (ArgumentException ex)
        {
            return new(null, ex.Message, null);
        }

        var transactionId = await GetTransactionId(chargePointId, connectorId, cancellationToken).ConfigureAwait(false);
        var remoteStartTransaction = new RemoteStopTransactionRequest()
        {
            TransactionId = transactionId,
        };
        try
        {
            var ocppResponse = await ocppWebSocketConnectionHandlingService.SendRequestAsync<RemoteStopTransactionResponse>(chargePointId,
                "RemoteStopTransaction",
                remoteStartTransaction,
                cancellationToken);
            if (ocppResponse.Status != RemoteStartStopStatus.Accepted)
            {
                logger.LogError("Error while sending RemoteStopTransaction to charge point {chargePointId}: Not Accepted", chargePointId);
                return new(ocppResponse, $"The Charge point {chargePointId} did not accept the request", null);
            }
            await UpdateLastSetValues(chargePointId, connectorId, 0, null, false, cancellationToken).ConfigureAwait(false);
            return new(ocppResponse, null, null);
        }
        catch (OcppCallErrorException ex)
        {
            logger.LogError(ex, "Error while sending RemoteStopTransaction to charge point {chargePointId}", chargePointId);
            return new(null, ex.Code + " " + ex.Description, null);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogError(ex, "Timeout while sending RemoteStopTransaction to charge point {chargePointId}", chargePointId);
            return new(null, ex.Message, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not send message to charge point {chargePointId} or charge point did not answer properly", chargePointId);
            return new(null, ex.Message, null);
        }
    }

    public async Task<Result<SetChargingProfileResponse?>> SetChargingCurrent(int chargingConnectorId, decimal currentToSet,
        int? numberOfPhases,
        CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({chargingConnectorId}, {currentToSet}, {numberOfPhases})", nameof(SetChargingCurrent), chargingConnectorId, currentToSet, numberOfPhases);
        var chargePointIdentifier = await GetChargePointIdentifierByChargingConnectorId(chargingConnectorId, cancellationToken).ConfigureAwait(false);
        return await SetChargingCurrent(chargePointIdentifier, currentToSet, numberOfPhases, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<SetChargingProfileResponse?>> SetChargingCurrent(string chargepointIdentifier, decimal currentToSet, int? numberOfPhases, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({chargePointIdentifier}, {currentToSet}, {numberOfPhases})", nameof(SetChargingCurrent), chargepointIdentifier, currentToSet, numberOfPhases);
        string chargePointId;
        int connectorId;
        try
        {
            chargePointId = GetChargePointAndConnectorId(chargepointIdentifier, out connectorId);
        }
        catch (ArgumentException ex)
        {
            return new(null, ex.Message, null);
        }

        var transactionId = await GetTransactionId(chargePointId, connectorId, cancellationToken).ConfigureAwait(false);
        var setChargingProfile = new SetChargingProfileRequest()
        {
            ConnectorId = connectorId,
            CsChargingProfiles = GenerateChargingProfile(false, currentToSet, numberOfPhases, transactionId),
        };
        try
        {
            var ocppResponse = await ocppWebSocketConnectionHandlingService.SendRequestAsync<SetChargingProfileResponse>(chargePointId,
                "SetChargingProfile",
                setChargingProfile,
                cancellationToken);
            if (ocppResponse.Status != ChargingProfileStatus.Accepted)
            {
                logger.LogError("Error while sending SetChargingProfile to charge point {chargePointId}. Status: {status}", chargePointId, ocppResponse.Status);
                return new(ocppResponse, $"The Charge point {chargePointId} did not accept the request", null);
            }
            await UpdateLastSetValues(chargePointId, connectorId, currentToSet, numberOfPhases, true, cancellationToken).ConfigureAwait(false);
            return new(ocppResponse, null, null);
        }
        catch (OcppCallErrorException ex)
        {
            logger.LogError(ex, "Error while sending SetChargingCurrent to charge point {chargePointId}", chargePointId);
            return new(null, ex.Code + " " + ex.Description, null);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogError(ex, "Timeout while sending SetChargingCurrent to charge point {chargePointId}", chargePointId);
            return new(null, ex.Message, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not send message to charge point {chargePointId} or charge point did not answer properly", chargePointId);
            return new(null, ex.Message, null);
        }
    }

    private async Task UpdateLastSetValues(string chargePointId, int connectorId, decimal setCurrent, int? phases, bool updateLastSetPhases, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({chargePointId}, {chargingConnectorId}, {setCurrent}, {phases})", nameof(UpdateLastSetValues), chargePointId, connectorId, setCurrent, phases);
        var chargingConnectorId = await GetDbChargingConnectorId(connectorId, chargePointId, cancellationToken).ConfigureAwait(false);
        if (settings.OcppConnectorStates.TryGetValue(chargingConnectorId, out var connectorState))
        {
            var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
            connectorState.LastSetCurrent.Update(currentDate, setCurrent);
            if (updateLastSetPhases)
            {
                connectorState.LastSetPhases.Update(currentDate, phases);
            }
        }
        else
        {
            logger.LogWarning("Could not find charging connector state for connector {dbChargingConnectorID}, can not update last set current", chargingConnectorId);
        }
    }

    private async Task<int> GetDbChargingConnectorId(int connectorId, string chargePointId, CancellationToken cancellationToken)
    {
        var dbChargingConnectorId = await context.OcppChargingStationConnectors
            .Where(c => c.ConnectorId == connectorId
                        && c.OcppChargingStation.ChargepointId == chargePointId)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
        return dbChargingConnectorId;
    }

    private async Task<string> GetChargePointIdentifierByChargingConnectorId(int chargingConnectorId,
        CancellationToken cancellationToken)
    {
        var delimiter = constants.OcppChargePointConnectorIdDelimiter;
        var chargePointData = await context.OcppChargingStationConnectors
            .Where(c => c.Id == chargingConnectorId)
            .Select(c => new
            {
                c.OcppChargingStation.ChargepointId,
                c.ConnectorId,
            })
            .FirstAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var chargePointIdentifier = chargePointData.ChargepointId + delimiter + chargePointData.ConnectorId;
        return chargePointIdentifier;
    }

    private async Task<int> GetTransactionId(string chargepointIdentifier, int connectorId, CancellationToken cancellationToken)
    {
        var transactionId = await context.OcppTransactions
            .Where(t => t.EndDate == null
                        && t.ChargingStationConnector.OcppChargingStation.ChargepointId == chargepointIdentifier
                        && t.ChargingStationConnector.ConnectorId == connectorId)
            .OrderByDescending(t => t.Id)
            .Select(t => t.Id)
            .FirstAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return transactionId;
    }

    private string GetChargePointAndConnectorId(string chargepointIdentifier, out int connectorId)
    {
        if (string.IsNullOrEmpty(chargepointIdentifier))
            throw new ArgumentNullException(nameof(chargepointIdentifier));

        // default connector
        connectorId = 1;

        var delimiter = constants.OcppChargePointConnectorIdDelimiter;
        // if your delimiter is a string, use LastIndexOf(string), otherwise this works for char
        var lastPos = chargepointIdentifier.LastIndexOf(delimiter, StringComparison.Ordinal);
        if (lastPos < 0)
        {
            // no delimiter found → whole identifier is the CP ID
            return chargepointIdentifier;
        }

        // split into two parts
        var chargePointId = chargepointIdentifier.Substring(0, lastPos);
        var connectorIdString = chargepointIdentifier.Substring(lastPos + delimiter.Length);

        // try parse connector
        if (!int.TryParse(connectorIdString, out connectorId))
        {
            logger.LogError(
                "Could not parse connectorId {connectorId} to valid integer",
                connectorIdString
            );
            throw new ArgumentException(
                $"Could not parse connectorId '{connectorIdString}' to valid integer"
            );
        }

        return chargePointId;
    }

    private ChargingProfile GenerateChargingProfile(bool isChargeStart, decimal currentToSet, int? numberOfPhases, int? transactionId = null)
    {
        logger.LogTrace("{method}({isChargeStart}, {currentToSet}, {numberOfPhases}, {transactionId})", nameof(GenerateChargingProfile), isChargeStart, currentToSet, numberOfPhases, transactionId);
        //Set Startdate to one minute earlier so definetly all other profiles get overriden
        var startDate = dateTimeProvider.UtcNow().AddMinutes(-1);
        var chargingProfile = new ChargingProfile()
        {
            ChargingProfileId = 1,
            TransactionId = transactionId,
            StackLevel = 0,
            ChargingProfilePurpose = ChargingProfilePurposeType.TxProfile,
            ChargingProfileKind = ChargingProfileKindType.Relative,
            ChargingSchedule = new ChargingSchedule()
            {
                ChargingRateUnit = ChargingRateUnitType.A,
                ChargingSchedulePeriod =
                {
                    new ChargingSchedulePeriod()
                    {
                        StartPeriodSeconds = 0,
                        Limit = Math.Round((double)currentToSet, 1),
                        NumberPhases = numberOfPhases,
                    },
                },
            },
        };
        if (isChargeStart)
        {
            chargingProfile.ChargingProfileKind = ChargingProfileKindType.Relative;
        }
        else
        {
            chargingProfile.ChargingProfileKind = ChargingProfileKindType.Absolute;
            chargingProfile.ValidFrom = startDate;
            chargingProfile.ChargingSchedule.StartSchedule = startDate;

        }
        return chargingProfile;
    }
}
