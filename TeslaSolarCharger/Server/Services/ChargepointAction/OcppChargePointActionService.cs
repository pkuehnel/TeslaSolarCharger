using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Ocpp;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services.ChargepointAction;

public class OcppChargePointActionService(ILogger<OcppChargePointActionService> logger,
    IConstants constants,
    IOcppWebSocketConnectionHandlingService ocppWebSocketConnectionHandlingService) : IChargePointActionService
{
    public async Task<Result<object?>> StartCharging(string chargepointIdentifier, decimal currentToSet, int numberOfPhases,
        CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({chargePointIdentifier}, {currentToSet})", nameof(StartCharging), chargepointIdentifier, currentToSet);
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
                return new(null, $"The Charge point {chargePointId} did not accept the request", null);
            }
            return new Result<object?>(null, null, null);
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

    public async Task<Result<object?>> StopCharging(string chargepointIdentifier, CancellationToken cancellationToken)
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

        var remoteStartTransaction = new RemoteStopTransactionRequest()
        {
            TransactionId = 1,//ToDo: Get the transaction id from the database
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
                return new(null, $"The Charge point {chargePointId} did not accept the request", null);
            }
            return new Result<object?>(null, null, null);
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

    public async Task<Result<object?>> SetChargingCurrent(string chargepointIdentifier, decimal currentToSet, int numberOfPhases, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({chargePointIdentifier}, {currentToSet})", nameof(SetChargingCurrent), chargepointIdentifier, currentToSet);
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
        var setChargingProfile = new SetChargingProfileRequest()
        {
            ConnectorId = connectorId,
            CsChargingProfiles = GenerateChargingProfile(currentToSet, numberOfPhases),
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
                return new(null, $"The Charge point {chargePointId} did not accept the request", null);
            }
            return new Result<object?>(null, null, null);
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

    private string GetChargePointAndConnectorId(string chargepointIdentifier, out int connectorId)
    {
        var splittedChargePointIdentifier = chargepointIdentifier.Split(constants.OcppChargePointConnectorIdDelimiter);
        var chargePointId = splittedChargePointIdentifier[0];
        connectorId = 1;
        if (splittedChargePointIdentifier.Length > 1)
        {
            var connectorIdString = splittedChargePointIdentifier[1];
            if (!int.TryParse(connectorIdString, out connectorId))
            {
                logger.LogError("Could not parse connectorId {connectorId} to valid integer", connectorIdString);
                throw new ArgumentException($"Could not parse connectorId {connectorIdString} to valid integer");
            }
        }

        return chargePointId;
    }

    private ChargingProfile GenerateChargingProfile(decimal currentToSet, int numberOfPhases)
    {
        logger.LogTrace("{method}({currentToSet})", nameof(GenerateChargingProfile), currentToSet);
        var chargingProfile = new ChargingProfile()
        {
            ChargingProfileId = 1,
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
