using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Converters;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Model.EntityFramework;

public class TeslaSolarChargerContext : DbContext, ITeslaSolarChargerContext
{
    public DbSet<ChargePrice> ChargePrices { get; set; } = null!;
    public DbSet<CachedCarState> CachedCarStates { get; set; } = null!;
    public DbSet<HandledCharge> HandledCharges { get; set; } = null!;
    public DbSet<PowerDistribution> PowerDistributions { get; set; } = null!;
    public DbSet<SpotPrice> SpotPrices { get; set; } = null!;
    public DbSet<BackendToken> BackendTokens { get; set; } = null!;
    public DbSet<TscConfiguration> TscConfigurations { get; set; } = null!;
    public DbSet<Car> Cars { get; set; } = null!;
    public DbSet<RestValueConfiguration> RestValueConfigurations { get; set; } = null!;
    public DbSet<RestValueConfigurationHeader> RestValueConfigurationHeaders { get; set; } = null!;
    public DbSet<RestValueResultConfiguration> RestValueResultConfigurations { get; set; } = null!;
    public DbSet<ChargingProcess> ChargingProcesses { get; set; } = null!;
    public DbSet<ChargingDetail> ChargingDetails { get; set; } = null!;
    public DbSet<ModbusConfiguration> ModbusConfigurations { get; set; } = null!;
    public DbSet<ModbusResultConfiguration> ModbusResultConfigurations { get; set; } = null!;
    public DbSet<MqttConfiguration> MqttConfigurations { get; set; } = null!;
    public DbSet<MqttResultConfiguration> MqttResultConfigurations { get; set; } = null!;
    public DbSet<BackendNotification> BackendNotifications { get; set; } = null!;
    public DbSet<LoggedError> LoggedErrors { get; set; } = null!;
    public DbSet<CarValueLog> CarValueLogs { get; set; } = null!;
    public DbSet<MeterValue> MeterValues { get; set; } = null!;
    public DbSet<SolarRadiation> SolarRadiations { get; set; } = null!;
    public DbSet<OcppChargingStation> OcppChargingStations { get; set; } = null!;
    public DbSet<OcppChargingStationConnector> OcppChargingStationConnectors { get; set; } = null!;
    public DbSet<OcppTransaction> OcppTransactions { get; set; } = null!;
    // ReSharper disable once UnassignedGetOnlyAutoProperty
    public string DbPath { get; }

    public void RejectChanges()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Modified:
                case EntityState.Deleted:
                    entry.State = EntityState.Modified; //Revert changes made to deleted entity.
                    entry.State = EntityState.Unchanged;
                    break;
                case EntityState.Added:
                    entry.State = EntityState.Detached;
                    break;
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
            v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var dateTimeNullableConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                
                if (entityType.ClrType == typeof(Car) && property.Name == nameof(Car.LatestTimeToReachSoC))
                {
                    continue;
                }
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(dateTimeConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(dateTimeNullableConverter);
                }
            }
        }

        var localDateTimeConverter = new LocalDateTimeConverter();

        modelBuilder.Entity<Car>()
            .Property(c => c.LatestTimeToReachSoC)
            .HasConversion(localDateTimeConverter);

        modelBuilder.Entity<ChargePrice>()
            .Property(c => c.EnergyProvider)
            .HasDefaultValue(EnergyProvider.OldTeslaSolarChargerConfig);

        modelBuilder.Entity<TscConfiguration>()
            .HasIndex(c => c.Key)
            .IsUnique();

        modelBuilder.Entity<Car>()
            .HasIndex(c => c.TeslaMateCarId)
            .IsUnique();

        modelBuilder.Entity<Car>()
            .HasIndex(c => c.Vin)
            .IsUnique();

        modelBuilder.Entity<RestValueConfigurationHeader>()
            .HasIndex(h => new { h.RestValueConfigurationId, h.Key })
            .IsUnique();

        var timeListToStringValueConverter = new ValueConverter<List<DateTime>, string?>(
            v => JsonConvert.SerializeObject(v),
            v => v == null ? new() : JsonConvert.DeserializeObject<List<DateTime>>(v) ?? new List<DateTime>()
        );

        var valueComparer = new ValueComparer<List<DateTime>>(
            (c1, c2) => c2 != null && c1 != null && c1.SequenceEqual(c2), // Determines equality
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())), // Calculates hash code
            c => c.ToList() // Makes a snapshot copy
        );

        var dateTimeOffsetToEpochMilliSecondsConverter = new ValueConverter<DateTimeOffset?, long?>(
            v => v == default ? default : v.Value.ToUnixTimeMilliseconds(),
            v => v == default ? default : DateTimeOffset.FromUnixTimeMilliseconds(v.Value)
            );

        modelBuilder.Entity<MeterValue>()
            .Property(m => m.Timestamp)
            .HasConversion(dateTimeOffsetToEpochMilliSecondsConverter);

        modelBuilder.Entity<SolarRadiation>()
            .Property(m => m.Start)
            .HasConversion(dateTimeOffsetToEpochMilliSecondsConverter);

        modelBuilder.Entity<SolarRadiation>()
            .Property(m => m.End)
            .HasConversion(dateTimeOffsetToEpochMilliSecondsConverter);

        modelBuilder.Entity<SolarRadiation>()
            .Property(m => m.CreatedAt)
            .HasConversion(dateTimeOffsetToEpochMilliSecondsConverter);

        modelBuilder.Entity<OcppTransaction>()
            .Property(m => m.StartDate)
            .HasConversion(dateTimeOffsetToEpochMilliSecondsConverter);

        modelBuilder.Entity<OcppTransaction>()
            .Property(m => m.EndDate)
            .HasConversion(dateTimeOffsetToEpochMilliSecondsConverter);


        modelBuilder.Entity<LoggedError>()
            .Property(e => e.FurtherOccurrences)
            .HasConversion(timeListToStringValueConverter)
            .Metadata.SetValueComparer(valueComparer);

        modelBuilder.Entity<Car>()
            .Property(e => e.WakeUpCalls)
            .HasConversion(timeListToStringValueConverter)
            .Metadata.SetValueComparer(valueComparer);

        modelBuilder.Entity<Car>()
            .Property(e => e.VehicleDataCalls)
            .HasConversion(timeListToStringValueConverter)
            .Metadata.SetValueComparer(valueComparer);

        modelBuilder.Entity<Car>()
            .Property(e => e.VehicleCalls)
            .HasConversion(timeListToStringValueConverter)
            .Metadata.SetValueComparer(valueComparer);

        modelBuilder.Entity<Car>()
            .Property(e => e.ChargeStartCalls)
            .HasConversion(timeListToStringValueConverter)
            .Metadata.SetValueComparer(valueComparer);

        modelBuilder.Entity<Car>()
            .Property(e => e.ChargeStopCalls)
            .HasConversion(timeListToStringValueConverter)
            .Metadata.SetValueComparer(valueComparer);

        modelBuilder.Entity<Car>()
            .Property(e => e.SetChargingAmpsCall)
            .HasConversion(timeListToStringValueConverter)
            .Metadata.SetValueComparer(valueComparer);

        modelBuilder.Entity<Car>()
            .Property(e => e.OtherCommandCalls)
            .HasConversion(timeListToStringValueConverter)
            .Metadata.SetValueComparer(valueComparer);

    }

#pragma warning disable CS8618
    public TeslaSolarChargerContext()
#pragma warning restore CS8618
    {
    }

#pragma warning disable CS8618
    public TeslaSolarChargerContext(DbContextOptions<TeslaSolarChargerContext> options)
#pragma warning restore CS8618
        : base(options)
    {
    }
}
