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
}
