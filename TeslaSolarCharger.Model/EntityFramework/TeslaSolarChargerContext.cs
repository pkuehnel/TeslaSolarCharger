using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources;

namespace TeslaSolarCharger.Model.EntityFramework;

public class TeslaSolarChargerContext : DbContext, ITeslaSolarChargerContext
{
    public DbSet<ChargePrice> ChargePrices { get; set; } = null!;
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
    public DbSet<OcppChargingStationConnectorValueLog> OcppChargingStationConnectorValueLogs { get; set; } = null!;
    public DbSet<CarChargingTarget> CarChargingTargets { get; set; } = null!;
    public DbSet<PvValueLog> PvValueLogs { get; set; } = null!;
    public DbSet<ChargingStationConnectorAllowedCar> ChargingStationConnectorAllowedCars { get; set; } = null!;
    // ReSharper disable once UnassignedGetOnlyAutoProperty
    public string DbPath { get; }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new())
    {
        await ValidateChargingProcessOverlapsAsync();
        //Workaround for https://github.com/dotnet/efcore/issues/29514
        await Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;", cancellationToken: cancellationToken);
        return await base.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateChargingProcessOverlapsAsync()
    {
        var modifiedChargingProcesses = ChangeTracker.Entries<ChargingProcess>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
            .Select(e => e.Entity)
            .ToList();

        // Get all modified charging process IDs for exclusion
        var modifiedIds = modifiedChargingProcesses.Select(cp => cp.Id).ToHashSet();

        foreach (var chargingProcess in modifiedChargingProcesses)
        {
            // Check car overlaps
            if (chargingProcess.CarId.HasValue)
            {
                // Check against other modified entities in memory
                var hasCarOverlapInMemory = modifiedChargingProcesses
                    .Where(cp => cp.Id != chargingProcess.Id)
                    .Where(cp => cp.CarId == chargingProcess.CarId)
                    .Any(cp =>
                        chargingProcess.StartDate < (cp.EndDate ?? DateTime.MaxValue) &&
                        cp.StartDate < (chargingProcess.EndDate ?? DateTime.MaxValue));

                if (hasCarOverlapInMemory)
                {
                    throw new InvalidOperationException(
                        $"Overlapping charging process detected for car {chargingProcess.CarId} (in pending changes). " +
                        $"ChargingProcessId: {chargingProcess.Id}, " +
                        $"Time range: {chargingProcess.StartDate:u} - {(chargingProcess.EndDate?.ToString("u") ?? "null")}");
                }

                // Check against database records that are NOT being modified
                var hasCarOverlapInDb = await ChargingProcesses
                    .Where(cp => !modifiedIds.Contains(cp.Id))  // Exclude all modified processes
                    .Where(cp => cp.CarId == chargingProcess.CarId)
                    .Where(cp =>
                        chargingProcess.StartDate < (cp.EndDate ?? DateTime.MaxValue) &&
                        cp.StartDate < (chargingProcess.EndDate ?? DateTime.MaxValue))
                    .AnyAsync();

                if (hasCarOverlapInDb)
                {
                    throw new InvalidOperationException(
                        $"Overlapping charging process detected for car {chargingProcess.CarId}. " +
                        $"ChargingProcessId: {chargingProcess.Id}, " +
                        $"Time range: {chargingProcess.StartDate:u} - {(chargingProcess.EndDate?.ToString("u") ?? "null")}");
                }
            }

            // Check connector overlaps
            if (chargingProcess.OcppChargingStationConnectorId.HasValue)
            {
                // Check against other modified entities in memory
                var hasConnectorOverlapInMemory = modifiedChargingProcesses
                    .Where(cp => cp.Id != chargingProcess.Id)
                    .Where(cp => cp.OcppChargingStationConnectorId == chargingProcess.OcppChargingStationConnectorId)
                    .Any(cp =>
                        chargingProcess.StartDate < (cp.EndDate ?? DateTime.MaxValue) &&
                        cp.StartDate < (chargingProcess.EndDate ?? DateTime.MaxValue));

                if (hasConnectorOverlapInMemory)
                {
                    throw new InvalidOperationException(
                        $"Overlapping charging process detected for connector {chargingProcess.OcppChargingStationConnectorId} (in pending changes). " +
                        $"Process ID: {chargingProcess.Id}, " +
                        $"Time range: {chargingProcess.StartDate:u} - {(chargingProcess.EndDate?.ToString("u") ?? "null")}");
                }

                // Check against database records that are NOT being modified
                var hasConnectorOverlapInDb = await ChargingProcesses
                    .Where(cp => !modifiedIds.Contains(cp.Id))  // Exclude all modified processes
                    .Where(cp => cp.OcppChargingStationConnectorId == chargingProcess.OcppChargingStationConnectorId)
                    .Where(cp =>
                        chargingProcess.StartDate < (cp.EndDate ?? DateTime.MaxValue) &&
                        cp.StartDate < (chargingProcess.EndDate ?? DateTime.MaxValue))
                    .AnyAsync();

                if (hasConnectorOverlapInDb)
                {
                    throw new InvalidOperationException(
                        $"Overlapping charging process detected for connector {chargingProcess.OcppChargingStationConnectorId}. " +
                        $"Process ID: {chargingProcess.Id}, " +
                        $"Time range: {chargingProcess.StartDate:u} - {(chargingProcess.EndDate?.ToString("u") ?? "null")}");
                }
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

        modelBuilder.Entity<Car>()
            .Property(c => c.ChargeMode)
            .HasDefaultValue(ChargeModeV2.Auto);

        modelBuilder.Entity<OcppChargingStationConnector>()
            .Property(c => c.ChargeMode)
            .HasDefaultValue(ChargeModeV2.Auto);

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

        modelBuilder.Entity<ChargingStationConnectorAllowedCar>(entity =>
        {
            entity.HasKey(e => new { e.CarId, e.OcppChargingStationConnectorId }); // composite key

            entity.HasOne(e => e.Car)
                .WithMany() // requires collection on Car
                .HasForeignKey(e => e.CarId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.OcppChargingStationConnector)
                .WithMany(conn => conn.AllowedCars)
                .HasForeignKey(e => e.OcppChargingStationConnectorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

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

        var timeSpanToTicksConverter = new ValueConverter<TimeSpan?, long?>(
            v => v == default ? default : v.Value.Ticks,
            v => v == default ? default : TimeSpan.FromTicks(v.Value)
        );

        var timeOnlyToMillisecondsOfDayConverter = new ValueConverter<TimeOnly?, long?>(
            v => v == default ? default : (long)v.Value.ToTimeSpan().TotalMilliseconds,
            v => v == default ? default : TimeOnly.FromTimeSpan(TimeSpan.FromMilliseconds(v.Value, 0)));

        var dateOnlyToEpochMilliSecondsConverter = new ValueConverter<DateOnly?, long?>(
            v => v == default ? default : new DateTimeOffset(v.Value, TimeOnly.MinValue, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            v => v == default ? default : DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(v.Value).Date)
        );

        var carEnumValue = (int)MeterValueKind.Car;
        modelBuilder.Entity<MeterValue>(entity =>
        {
            //When changing thiss, also change the index in MeterValueDatabaseSaveJob
            entity.HasIndex(m => new { m.CarId, m.ChargingConnectorId, m.MeterValueKind, m.Timestamp })
                .HasDatabaseName(StaticConstants.MeterValueIndexName);

            entity.Property(m => m.Timestamp)
                .HasConversion(dateTimeOffsetToEpochMilliSecondsConverter);

            entity.Property(m => m.NormalizeInterval)
                .HasConversion(timeSpanToTicksConverter);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_MeterValue_CarId_Conditional",
                    $"(MeterValueKind = {carEnumValue} AND CarId IS NOT NULL) OR (MeterValueKind != {carEnumValue} AND CarId IS NULL)"
                );
            });
        });

        modelBuilder.Entity<CarValueLog>()
            .HasIndex(m => new { m.CarId, m.Type, m.Timestamp });

        modelBuilder.Entity<PvValueLog>()
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

        modelBuilder.Entity<CarChargingTarget>()
            .Property(m => m.TargetTime)
            .HasConversion(timeOnlyToMillisecondsOfDayConverter);

        modelBuilder.Entity<CarChargingTarget>()
            .Property(m => m.TargetDate)
            .HasConversion(dateOnlyToEpochMilliSecondsConverter);

        modelBuilder.Entity<CarChargingTarget>()
            .Property(m => m.LastFulFilled)
            .HasConversion(dateTimeOffsetToEpochMilliSecondsConverter);

        modelBuilder.Entity<OcppChargingStationConnectorValueLog>()
            .Property(m => m.Timestamp)
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
