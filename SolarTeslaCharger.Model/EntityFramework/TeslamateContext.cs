using Microsoft.EntityFrameworkCore;
using SolarTeslaCharger.Model.Contracts;
using SolarTeslaCharger.Model.Entities;

namespace SolarTeslaCharger.Model.EntityFramework
{
    public class TeslamateContext : DbContext, ITeslamateContext
    {
        public TeslamateContext()
        {
        }

        public TeslamateContext(DbContextOptions<TeslamateContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Address> Addresses { get; set; } = null!;
        public virtual DbSet<Car> Cars { get; set; } = null!;
        public virtual DbSet<CarSetting> CarSettings { get; set; } = null!;
        public virtual DbSet<Charge> Charges { get; set; } = null!;
        public virtual DbSet<ChargingProcess> ChargingProcesses { get; set; } = null!;
        public virtual DbSet<Drive> Drives { get; set; } = null!;
        public virtual DbSet<Geofence> Geofences { get; set; } = null!;
        public virtual DbSet<Position> Positions { get; set; } = null!;
        public virtual DbSet<SchemaMigration> SchemaMigrations { get; set; } = null!;
        public virtual DbSet<Setting> Settings { get; set; } = null!;
        public virtual DbSet<State> States { get; set; } = null!;
        public virtual DbSet<Token> Tokens { get; set; } = null!;
        public virtual DbSet<Update> Updates { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                //optionsBuilder.UseNpgsql("Host=192.168.1.50;Port=5433;Database=teslamate;Username=teslamate;Password=secret");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresEnum("billing_type", new[] { "per_kwh", "per_minute" })
                .HasPostgresEnum("range", new[] { "ideal", "rated" })
                .HasPostgresEnum("states_status", new[] { "online", "offline", "asleep" })
                .HasPostgresEnum("unit_of_length", new[] { "km", "mi" })
                .HasPostgresEnum("unit_of_temperature", new[] { "C", "F" })
                .HasPostgresExtension("cube")
                .HasPostgresExtension("earthdistance");

            modelBuilder.Entity<Address>(entity =>
            {
                entity.ToTable("addresses");

                entity.HasIndex(e => new { e.OsmId, e.OsmType }, "addresses_osm_id_osm_type_index")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.City)
                    .HasMaxLength(255)
                    .HasColumnName("city");

                entity.Property(e => e.Country)
                    .HasMaxLength(255)
                    .HasColumnName("country");

                entity.Property(e => e.County)
                    .HasMaxLength(255)
                    .HasColumnName("county");

                entity.Property(e => e.DisplayName)
                    .HasMaxLength(512)
                    .HasColumnName("display_name");

                entity.Property(e => e.HouseNumber)
                    .HasMaxLength(255)
                    .HasColumnName("house_number");

                entity.Property(e => e.InsertedAt)
                    .HasColumnType("timestamp(0) without time zone")
                    .HasColumnName("inserted_at");

                entity.Property(e => e.Latitude)
                    .HasPrecision(8, 6)
                    .HasColumnName("latitude");

                entity.Property(e => e.Longitude)
                    .HasPrecision(9, 6)
                    .HasColumnName("longitude");

                entity.Property(e => e.Name)
                    .HasMaxLength(255)
                    .HasColumnName("name");

                entity.Property(e => e.Neighbourhood)
                    .HasMaxLength(255)
                    .HasColumnName("neighbourhood");

                entity.Property(e => e.OsmId).HasColumnName("osm_id");

                entity.Property(e => e.OsmType).HasColumnName("osm_type");

                entity.Property(e => e.Postcode)
                    .HasMaxLength(255)
                    .HasColumnName("postcode");

                entity.Property(e => e.Raw)
                    .HasColumnType("jsonb")
                    .HasColumnName("raw");

                entity.Property(e => e.Road)
                    .HasMaxLength(255)
                    .HasColumnName("road");

                entity.Property(e => e.State)
                    .HasMaxLength(255)
                    .HasColumnName("state");

                entity.Property(e => e.StateDistrict)
                    .HasMaxLength(255)
                    .HasColumnName("state_district");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("timestamp(0) without time zone")
                    .HasColumnName("updated_at");
            });

            modelBuilder.Entity<Car>(entity =>
            {
                entity.ToTable("cars");

                entity.HasIndex(e => e.Eid, "cars_eid_index")
                    .IsUnique();

                entity.HasIndex(e => e.SettingsId, "cars_settings_id_index")
                    .IsUnique();

                entity.HasIndex(e => e.Vid, "cars_vid_index")
                    .IsUnique();

                entity.HasIndex(e => e.Vin, "cars_vin_index")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.DisplayPriority)
                    .HasColumnName("display_priority")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.Efficiency).HasColumnName("efficiency");

                entity.Property(e => e.Eid).HasColumnName("eid");

                entity.Property(e => e.ExteriorColor).HasColumnName("exterior_color");

                entity.Property(e => e.InsertedAt)
                    .HasColumnType("timestamp(0) without time zone")
                    .HasColumnName("inserted_at");

                entity.Property(e => e.Model)
                    .HasMaxLength(255)
                    .HasColumnName("model");

                entity.Property(e => e.Name).HasColumnName("name");

                entity.Property(e => e.SettingsId).HasColumnName("settings_id");

                entity.Property(e => e.SpoilerType).HasColumnName("spoiler_type");

                entity.Property(e => e.TrimBadging).HasColumnName("trim_badging");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("timestamp(0) without time zone")
                    .HasColumnName("updated_at");

                entity.Property(e => e.Vid).HasColumnName("vid");

                entity.Property(e => e.Vin).HasColumnName("vin");

                entity.Property(e => e.WheelType).HasColumnName("wheel_type");

                entity.HasOne(d => d.Settings)
                    .WithOne(p => p.Car)
                    .HasForeignKey<Car>(d => d.SettingsId)
                    .HasConstraintName("cars_settings_id_fkey");
            });

            modelBuilder.Entity<CarSetting>(entity =>
            {
                entity.ToTable("car_settings");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.FreeSupercharging).HasColumnName("free_supercharging");

                entity.Property(e => e.ReqNotUnlocked).HasColumnName("req_not_unlocked");

                entity.Property(e => e.SuspendAfterIdleMin)
                    .HasColumnName("suspend_after_idle_min")
                    .HasDefaultValueSql("15");

                entity.Property(e => e.SuspendMin)
                    .HasColumnName("suspend_min")
                    .HasDefaultValueSql("21");

                entity.Property(e => e.UseStreamingApi)
                    .IsRequired()
                    .HasColumnName("use_streaming_api")
                    .HasDefaultValueSql("true");
            });

            modelBuilder.Entity<Charge>(entity =>
            {
                entity.ToTable("charges");

                entity.HasIndex(e => e.ChargingProcessId, "charges_charging_process_id_index");

                entity.HasIndex(e => e.Date, "charges_date_index");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.BatteryHeater).HasColumnName("battery_heater");

                entity.Property(e => e.BatteryHeaterNoPower).HasColumnName("battery_heater_no_power");

                entity.Property(e => e.BatteryHeaterOn).HasColumnName("battery_heater_on");

                entity.Property(e => e.BatteryLevel).HasColumnName("battery_level");

                entity.Property(e => e.ChargeEnergyAdded)
                    .HasPrecision(8, 2)
                    .HasColumnName("charge_energy_added");

                entity.Property(e => e.ChargerActualCurrent).HasColumnName("charger_actual_current");

                entity.Property(e => e.ChargerPhases).HasColumnName("charger_phases");

                entity.Property(e => e.ChargerPilotCurrent).HasColumnName("charger_pilot_current");

                entity.Property(e => e.ChargerPower).HasColumnName("charger_power");

                entity.Property(e => e.ChargerVoltage).HasColumnName("charger_voltage");

                entity.Property(e => e.ChargingProcessId).HasColumnName("charging_process_id");

                entity.Property(e => e.ConnChargeCable)
                    .HasMaxLength(255)
                    .HasColumnName("conn_charge_cable");

                entity.Property(e => e.Date)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("date");

                entity.Property(e => e.FastChargerBrand)
                    .HasMaxLength(255)
                    .HasColumnName("fast_charger_brand");

                entity.Property(e => e.FastChargerPresent).HasColumnName("fast_charger_present");

                entity.Property(e => e.FastChargerType)
                    .HasMaxLength(255)
                    .HasColumnName("fast_charger_type");

                entity.Property(e => e.IdealBatteryRangeKm)
                    .HasPrecision(6, 2)
                    .HasColumnName("ideal_battery_range_km");

                entity.Property(e => e.NotEnoughPowerToHeat).HasColumnName("not_enough_power_to_heat");

                entity.Property(e => e.OutsideTemp)
                    .HasPrecision(4, 1)
                    .HasColumnName("outside_temp");

                entity.Property(e => e.RatedBatteryRangeKm)
                    .HasPrecision(6, 2)
                    .HasColumnName("rated_battery_range_km");

                entity.Property(e => e.UsableBatteryLevel).HasColumnName("usable_battery_level");

                entity.HasOne(d => d.ChargingProcess)
                    .WithMany(p => p.Charges)
                    .HasForeignKey(d => d.ChargingProcessId)
                    .HasConstraintName("charges_charging_process_id_fkey");
            });

            modelBuilder.Entity<ChargingProcess>(entity =>
            {
                entity.ToTable("charging_processes");

                entity.HasIndex(e => e.AddressId, "charging_processes_address_id_index");

                entity.HasIndex(e => e.CarId, "charging_processes_car_id_index");

                entity.HasIndex(e => e.PositionId, "charging_processes_position_id_index");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.AddressId).HasColumnName("address_id");

                entity.Property(e => e.CarId).HasColumnName("car_id");

                entity.Property(e => e.ChargeEnergyAdded)
                    .HasPrecision(8, 2)
                    .HasColumnName("charge_energy_added");

                entity.Property(e => e.ChargeEnergyUsed)
                    .HasPrecision(8, 2)
                    .HasColumnName("charge_energy_used");

                entity.Property(e => e.Cost)
                    .HasPrecision(6, 2)
                    .HasColumnName("cost");

                entity.Property(e => e.DurationMin).HasColumnName("duration_min");

                entity.Property(e => e.EndBatteryLevel).HasColumnName("end_battery_level");

                entity.Property(e => e.EndDate)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("end_date");

                entity.Property(e => e.EndIdealRangeKm)
                    .HasPrecision(6, 2)
                    .HasColumnName("end_ideal_range_km");

                entity.Property(e => e.EndRatedRangeKm)
                    .HasPrecision(6, 2)
                    .HasColumnName("end_rated_range_km");

                entity.Property(e => e.GeofenceId).HasColumnName("geofence_id");

                entity.Property(e => e.OutsideTempAvg)
                    .HasPrecision(4, 1)
                    .HasColumnName("outside_temp_avg");

                entity.Property(e => e.PositionId).HasColumnName("position_id");

                entity.Property(e => e.StartBatteryLevel).HasColumnName("start_battery_level");

                entity.Property(e => e.StartDate)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("start_date");

                entity.Property(e => e.StartIdealRangeKm)
                    .HasPrecision(6, 2)
                    .HasColumnName("start_ideal_range_km");

                entity.Property(e => e.StartRatedRangeKm)
                    .HasPrecision(6, 2)
                    .HasColumnName("start_rated_range_km");

                entity.HasOne(d => d.Address)
                    .WithMany(p => p.ChargingProcesses)
                    .HasForeignKey(d => d.AddressId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("charging_processes_address_id_fkey");

                entity.HasOne(d => d.Car)
                    .WithMany(p => p.ChargingProcesses)
                    .HasForeignKey(d => d.CarId)
                    .HasConstraintName("charging_processes_car_id_fkey");

                entity.HasOne(d => d.Geofence)
                    .WithMany(p => p.ChargingProcesses)
                    .HasForeignKey(d => d.GeofenceId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("charging_processes_geofence_id_fkey");

                entity.HasOne(d => d.Position)
                    .WithMany(p => p.ChargingProcesses)
                    .HasForeignKey(d => d.PositionId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("charging_processes_position_id_fkey");
            });

            modelBuilder.Entity<Drive>(entity =>
            {
                entity.ToTable("drives");

                entity.HasIndex(e => e.EndGeofenceId, "drives_end_geofence_id_index");

                entity.HasIndex(e => e.EndPositionId, "drives_end_position_id_index");

                entity.HasIndex(e => e.StartGeofenceId, "drives_start_geofence_id_index");

                entity.HasIndex(e => e.StartPositionId, "drives_start_position_id_index");

                entity.HasIndex(e => e.CarId, "trips_car_id_index");

                entity.HasIndex(e => e.EndAddressId, "trips_end_address_id_index");

                entity.HasIndex(e => e.StartAddressId, "trips_start_address_id_index");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasDefaultValueSql("nextval('trips_id_seq'::regclass)");

                entity.Property(e => e.CarId).HasColumnName("car_id");

                entity.Property(e => e.Distance).HasColumnName("distance");

                entity.Property(e => e.DurationMin).HasColumnName("duration_min");

                entity.Property(e => e.EndAddressId).HasColumnName("end_address_id");

                entity.Property(e => e.EndDate)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("end_date");

                entity.Property(e => e.EndGeofenceId).HasColumnName("end_geofence_id");

                entity.Property(e => e.EndIdealRangeKm)
                    .HasPrecision(6, 2)
                    .HasColumnName("end_ideal_range_km");

                entity.Property(e => e.EndKm).HasColumnName("end_km");

                entity.Property(e => e.EndPositionId).HasColumnName("end_position_id");

                entity.Property(e => e.EndRatedRangeKm)
                    .HasPrecision(6, 2)
                    .HasColumnName("end_rated_range_km");

                entity.Property(e => e.InsideTempAvg)
                    .HasPrecision(4, 1)
                    .HasColumnName("inside_temp_avg");

                entity.Property(e => e.OutsideTempAvg)
                    .HasPrecision(4, 1)
                    .HasColumnName("outside_temp_avg");

                entity.Property(e => e.PowerMax).HasColumnName("power_max");

                entity.Property(e => e.PowerMin).HasColumnName("power_min");

                entity.Property(e => e.SpeedMax).HasColumnName("speed_max");

                entity.Property(e => e.StartAddressId).HasColumnName("start_address_id");

                entity.Property(e => e.StartDate)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("start_date");

                entity.Property(e => e.StartGeofenceId).HasColumnName("start_geofence_id");

                entity.Property(e => e.StartIdealRangeKm)
                    .HasPrecision(6, 2)
                    .HasColumnName("start_ideal_range_km");

                entity.Property(e => e.StartKm).HasColumnName("start_km");

                entity.Property(e => e.StartPositionId).HasColumnName("start_position_id");

                entity.Property(e => e.StartRatedRangeKm)
                    .HasPrecision(6, 2)
                    .HasColumnName("start_rated_range_km");

                entity.HasOne(d => d.Car)
                    .WithMany(p => p.Drives)
                    .HasForeignKey(d => d.CarId)
                    .HasConstraintName("drives_car_id_fkey");

                entity.HasOne(d => d.EndAddress)
                    .WithMany(p => p.DriveEndAddresses)
                    .HasForeignKey(d => d.EndAddressId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("drives_end_address_id_fkey");

                entity.HasOne(d => d.EndGeofence)
                    .WithMany(p => p.DriveEndGeofences)
                    .HasForeignKey(d => d.EndGeofenceId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("drives_end_geofence_id_fkey");

                entity.HasOne(d => d.EndPosition)
                    .WithMany(p => p.DriveEndPositions)
                    .HasForeignKey(d => d.EndPositionId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("drives_end_position_id_fkey");

                entity.HasOne(d => d.StartAddress)
                    .WithMany(p => p.DriveStartAddresses)
                    .HasForeignKey(d => d.StartAddressId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("drives_start_address_id_fkey");

                entity.HasOne(d => d.StartGeofence)
                    .WithMany(p => p.DriveStartGeofences)
                    .HasForeignKey(d => d.StartGeofenceId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("drives_start_geofence_id_fkey");

                entity.HasOne(d => d.StartPosition)
                    .WithMany(p => p.DriveStartPositions)
                    .HasForeignKey(d => d.StartPositionId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("drives_start_position_id_fkey");
            });

            modelBuilder.Entity<Geofence>(entity =>
            {
                entity.ToTable("geofences");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.CostPerUnit)
                    .HasPrecision(6, 4)
                    .HasColumnName("cost_per_unit");

                entity.Property(e => e.InsertedAt)
                    .HasColumnType("timestamp(0) without time zone")
                    .HasColumnName("inserted_at");

                entity.Property(e => e.Latitude)
                    .HasPrecision(8, 6)
                    .HasColumnName("latitude");

                entity.Property(e => e.Longitude)
                    .HasPrecision(9, 6)
                    .HasColumnName("longitude");

                entity.Property(e => e.Name)
                    .HasMaxLength(255)
                    .HasColumnName("name");

                entity.Property(e => e.Radius)
                    .HasColumnName("radius")
                    .HasDefaultValueSql("25");

                entity.Property(e => e.SessionFee)
                    .HasPrecision(6, 2)
                    .HasColumnName("session_fee");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("timestamp(0) without time zone")
                    .HasColumnName("updated_at");
            });

            modelBuilder.Entity<Position>(entity =>
            {
                entity.ToTable("positions");

                entity.HasIndex(e => e.CarId, "positions_car_id_index");

                entity.HasIndex(e => e.Date, "positions_date_index");

                entity.HasIndex(e => e.DriveId, "positions_drive_id_index");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.BatteryHeater).HasColumnName("battery_heater");

                entity.Property(e => e.BatteryHeaterNoPower).HasColumnName("battery_heater_no_power");

                entity.Property(e => e.BatteryHeaterOn).HasColumnName("battery_heater_on");

                entity.Property(e => e.BatteryLevel).HasColumnName("battery_level");

                entity.Property(e => e.CarId).HasColumnName("car_id");

                entity.Property(e => e.Date)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("date");

                entity.Property(e => e.DriveId).HasColumnName("drive_id");

                entity.Property(e => e.DriverTempSetting)
                    .HasPrecision(4, 1)
                    .HasColumnName("driver_temp_setting");

                entity.Property(e => e.Elevation).HasColumnName("elevation");

                entity.Property(e => e.EstBatteryRangeKm)
                    .HasPrecision(6, 2)
                    .HasColumnName("est_battery_range_km");

                entity.Property(e => e.FanStatus).HasColumnName("fan_status");

                entity.Property(e => e.IdealBatteryRangeKm)
                    .HasPrecision(6, 2)
                    .HasColumnName("ideal_battery_range_km");

                entity.Property(e => e.InsideTemp)
                    .HasPrecision(4, 1)
                    .HasColumnName("inside_temp");

                entity.Property(e => e.IsClimateOn).HasColumnName("is_climate_on");

                entity.Property(e => e.IsFrontDefrosterOn).HasColumnName("is_front_defroster_on");

                entity.Property(e => e.IsRearDefrosterOn).HasColumnName("is_rear_defroster_on");

                entity.Property(e => e.Latitude)
                    .HasPrecision(8, 6)
                    .HasColumnName("latitude");

                entity.Property(e => e.Longitude)
                    .HasPrecision(9, 6)
                    .HasColumnName("longitude");

                entity.Property(e => e.Odometer).HasColumnName("odometer");

                entity.Property(e => e.OutsideTemp)
                    .HasPrecision(4, 1)
                    .HasColumnName("outside_temp");

                entity.Property(e => e.PassengerTempSetting)
                    .HasPrecision(4, 1)
                    .HasColumnName("passenger_temp_setting");

                entity.Property(e => e.Power).HasColumnName("power");

                entity.Property(e => e.RatedBatteryRangeKm)
                    .HasPrecision(6, 2)
                    .HasColumnName("rated_battery_range_km");

                entity.Property(e => e.Speed).HasColumnName("speed");

                entity.Property(e => e.UsableBatteryLevel).HasColumnName("usable_battery_level");

                entity.HasOne(d => d.Car)
                    .WithMany(p => p.Positions)
                    .HasForeignKey(d => d.CarId)
                    .HasConstraintName("positions_car_id_fkey");

                entity.HasOne(d => d.Drive)
                    .WithMany(p => p.Positions)
                    .HasForeignKey(d => d.DriveId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("positions_drive_id_fkey");
            });

            modelBuilder.Entity<SchemaMigration>(entity =>
            {
                entity.HasKey(e => e.Version)
                    .HasName("schema_migrations_pkey");

                entity.ToTable("schema_migrations");

                entity.Property(e => e.Version)
                    .ValueGeneratedNever()
                    .HasColumnName("version");

                entity.Property(e => e.InsertedAt)
                    .HasColumnType("timestamp(0) without time zone")
                    .HasColumnName("inserted_at");
            });

            modelBuilder.Entity<Setting>(entity =>
            {
                entity.ToTable("settings");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.BaseUrl)
                    .HasMaxLength(255)
                    .HasColumnName("base_url");

                entity.Property(e => e.GrafanaUrl)
                    .HasMaxLength(255)
                    .HasColumnName("grafana_url");

                entity.Property(e => e.InsertedAt)
                    .HasColumnType("timestamp(0) without time zone")
                    .HasColumnName("inserted_at");

                entity.Property(e => e.Language)
                    .HasColumnName("language")
                    .HasDefaultValueSql("'en'::text");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("timestamp(0) without time zone")
                    .HasColumnName("updated_at");
            });

            modelBuilder.Entity<State>(entity =>
            {
                entity.ToTable("states");

                entity.HasIndex(e => e.CarId, "states_car_id_index");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.CarId).HasColumnName("car_id");

                entity.Property(e => e.EndDate)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("end_date");

                entity.Property(e => e.StartDate)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("start_date");

                entity.HasOne(d => d.Car)
                    .WithMany(p => p.States)
                    .HasForeignKey(d => d.CarId)
                    .HasConstraintName("states_car_id_fkey");
            });

            modelBuilder.Entity<Token>(entity =>
            {
                entity.ToTable("tokens");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Access).HasColumnName("access");

                entity.Property(e => e.InsertedAt)
                    .HasColumnType("timestamp(0) without time zone")
                    .HasColumnName("inserted_at");

                entity.Property(e => e.Refresh).HasColumnName("refresh");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("timestamp(0) without time zone")
                    .HasColumnName("updated_at");
            });

            modelBuilder.Entity<Update>(entity =>
            {
                entity.ToTable("updates");

                entity.HasIndex(e => e.CarId, "updates_car_id_index");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.CarId).HasColumnName("car_id");

                entity.Property(e => e.EndDate)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("end_date");

                entity.Property(e => e.StartDate)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("start_date");

                entity.Property(e => e.Version)
                    .HasMaxLength(255)
                    .HasColumnName("version");

                entity.HasOne(d => d.Car)
                    .WithMany(p => p.Updates)
                    .HasForeignKey(d => d.CarId)
                    .HasConstraintName("updates_car_id_fkey");
            });
        }
    }
}
