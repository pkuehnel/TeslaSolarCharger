using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

namespace TeslaSolarCharger.Model.Contracts;

public interface ITeslaSolarChargerContext
{
    DbSet<ChargePrice> ChargePrices { get; set; }
    DbSet<HandledCharge> HandledCharges { get; set; }
    DbSet<PowerDistribution> PowerDistributions { get; set; }
    ChangeTracker ChangeTracker { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
