using Microsoft.EntityFrameworkCore;
using SolarTeslaCharger.Model.Entities;

namespace SolarTeslaCharger.Model.Contracts;

public interface ITeslamateContext
{
    DbSet<Address> Addresses { get; set; }
    DbSet<Car> Cars { get; set; }
    DbSet<CarSetting> CarSettings { get; set; }
    DbSet<Charge> Charges { get; set; }
    DbSet<ChargingProcess> ChargingProcesses { get; set; }
    DbSet<Drive> Drives { get; set; }
    DbSet<Geofence> Geofences { get; set; }
    DbSet<Position> Positions { get; set; }
    DbSet<SchemaMigration> SchemaMigrations { get; set; }
    DbSet<Setting> Settings { get; set; }
    DbSet<State> States { get; set; }
    DbSet<Token> Tokens { get; set; }
    DbSet<Update> Updates { get; set; }
}