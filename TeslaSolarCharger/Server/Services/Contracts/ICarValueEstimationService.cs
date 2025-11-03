namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ICarValueEstimationService
{
    Task UpdateAllCarValueEstimations(CancellationToken cancellationToken);
    Task PlugoutCarsAndClearSocIfRequired(CancellationToken cancellationToken);
}
