using TeslaSolarCharger.Shared.Dtos.UsableEnergy;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IUsableEnergyLearningService
{
    /// <summary>
    /// Turns charges and home battery runs that were not measured yet into samples and updates the learned values.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Recalculates the learned values from the stored samples and puts them where the planning reads them.
    /// </summary>
    Task UpdateLearnedValuesAsync(CancellationToken cancellationToken);

    Task<DtoLearnedUsableEnergy> GetCarStateAsync(int carId, CancellationToken cancellationToken);
    Task<DtoLearnedUsableEnergy> GetHomeBatteryStateAsync(CancellationToken cancellationToken);

    /// <summary>Forgets everything learned about the car's battery and learns again from the next charge.</summary>
    Task ResetCarAsync(int carId, CancellationToken cancellationToken);

    /// <summary>Forgets everything learned about the home battery and learns again from now on.</summary>
    Task ResetHomeBatteryAsync(CancellationToken cancellationToken);
}
