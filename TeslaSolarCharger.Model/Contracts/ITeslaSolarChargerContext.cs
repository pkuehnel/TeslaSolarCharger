using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

namespace TeslaSolarCharger.Model.Contracts;

public interface ITeslaSolarChargerContext
{
    DbSet<ChargePrice> ChargePrices { get; set; }
    DbSet<CachedCarState> CachedCarStates { get; set; }
    DbSet<HandledCharge> HandledCharges { get; set; }
    DbSet<PowerDistribution> PowerDistributions { get; set; }
    ChangeTracker ChangeTracker { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken());
    DatabaseFacade Database { get; }
    DbSet<SpotPrice> SpotPrices { get; set; }
    DbSet<BackendToken> BackendTokens { get; set; }
    DbSet<TscConfiguration> TscConfigurations { get; set; }
    DbSet<Car> Cars { get; set; }
    DbSet<RestValueConfiguration> RestValueConfigurations { get; set; }
    DbSet<RestValueConfigurationHeader> RestValueConfigurationHeaders { get; set; }
    DbSet<RestValueResultConfiguration> RestValueResultConfigurations { get; set; }
    DbSet<ChargingProcess> ChargingProcesses { get; set; }
    DbSet<ChargingDetail> ChargingDetails { get; set; }
    DbSet<ModbusConfiguration> ModbusConfigurations { get; set; }
    DbSet<ModbusResultConfiguration> ModbusResultConfigurations { get; set; }
    DbSet<MqttConfiguration> MqttConfigurations { get; set; }
    DbSet<MqttResultConfiguration> MqttResultConfigurations { get; set; }
    DbSet<BackendNotification> BackendNotifications { get; set; }
    DbSet<LoggedError> LoggedErrors { get; set; }
    DbSet<CarValueLog> CarValueLogs { get; set; }
    DbSet<MeterValue> MeterValues { get; set; }
    DbSet<SolarRadiation> SolarRadiations { get; set; }
    void RejectChanges();
}
