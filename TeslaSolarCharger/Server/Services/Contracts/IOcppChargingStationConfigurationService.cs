namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IOcppChargingStationConfigurationService
{
    Task AddChargingStationIfNotExisting(string chargepointId, CancellationToken httpContextRequestAborted);
}
