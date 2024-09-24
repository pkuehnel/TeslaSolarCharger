using LanguageExt;
using TeslaSolarCharger.Server.Services.GridPrice.Dtos;
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
    Task<IEnumerable<Price>> GetPriceData(DateTimeOffset from, DateTimeOffset to);
    Task<string> GetInstallationId();
    Dictionary<int, string> GetRawRestRequestResults();
    Dictionary<int, string> GetRawRestValue();
    Dictionary<int, decimal?> GetCalculatedRestValue();
    bool IsStartupCompleted();
    Task<Fin<DtoValue<string>>> SendTestTelegramMessage();
}
