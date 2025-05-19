using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Ocpp;
using TeslaSolarCharger.Server.Dtos.Ocpp.Generics;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services.ChargepointAction;

public class OcppChargePointActionService(ILogger<OcppChargePointActionService> logger,
    IConstants constants,
    IOcppWebSocketConnectionHandlingService ocppWebSocketConnectionHandlingService,
    ITeslaSolarChargerContext context) : IChargePointActionService
{
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
        
        var remoteStartTransaction = new RemoteStartTransactionRequest()
        {
            ConnectorId = connectorId,
            IdTag = constants.DefaultIdTag,
            ChargingProfile = GenerateChargingProfile(currentToSet, numberOfPhases),
        };
        try
        {
            var ocppResponse = await ocppWebSocketConnectionHandlingService.SendRequestAsync<RemoteStartTransactionResponse>(chargePointId,
                "RemoteStartTransaction",
                remoteStartTransaction,
                cancellationToken);
            if(ocppResponse.Status != RemoteStartStopStatus.Accepted)
            {
                logger.LogError("Error while sending RemoteStartTransaction to charge point {chargePointId}: Not Accepted", chargePointId);
                return new(ocppResponse, $"The Charge point {chargePointId} did not accept the request", null);
            }
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
            CsChargingProfiles = GenerateChargingProfile(currentToSet, numberOfPhases, transactionId),
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

    private ChargingProfile GenerateChargingProfile(decimal currentToSet, int? numberOfPhases, int? transactionId = null)
    {
        logger.LogTrace("{method}({currentToSet}, {numberOfPhases}, {transactionId})", nameof(GenerateChargingProfile), currentToSet, numberOfPhases, transactionId);
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
                        Limit = (double)currentToSet,
                        NumberPhases = numberOfPhases,
                    },
                },
            },
        };
        return chargingProfile;
    }
}
