using TeslaSolarCharger.GridPriceProvider.Data;
using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Server.Contracts;

public interface ICoreService
{
    Task<string?> GetCurrentVersion();
    void LogVersion();
    DtoValue<int> NumberOfRelevantCars();
    DtoValue<int> HomeBatteryTargetChargingPower();
    DtoValue<bool> IsSolarEdgeInstallation();
    DateTime GetCurrentServerTime();
    DtoValue<string> GetServerTimeZoneDisplayName();
    Task BackupDatabaseIfNeeded();
    Task KillAllServices();
    Task StopJobs();
    Task DisconnectMqttServices();
    DtoValue<int> TeslaApiRequestsSinceStartup();
    DtoValue<bool> ShouldDisplayApiRequestCounter();
    Task<IEnumerable<Price>> GetPriceData(DateTimeOffset from, DateTimeOffset to);
}
