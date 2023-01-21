using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

namespace TeslaSolarCharger.Model.EntityFramework;

public class TeslaSolarChargerContext : DbContext, ITeslaSolarChargerContext
{
    public DbSet<ChargePrice> ChargePrices { get; set; } = null!;
    public DbSet<CachedCarState> CachedCarStates { get; set; } = null!;
    public DbSet<HandledCharge> HandledCharges { get; set; } = null!;
    public DbSet<PowerDistribution> PowerDistributions { get; set; } = null!;
    public DbSet<SpotPrice> SpotPrices { get; set; } = null!;

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
