namespace TeslaSolarCharger.Shared.Dtos.UsableEnergy;

/// <summary>Learning state of one battery, shown next to the entered usable energy.</summary>
public class DtoLearnedUsableEnergy
{
    public bool LearningEnabled { get; set; }
    /// <summary>The summed SoC of several home batteries does not describe one battery, so nothing is learned.</summary>
    public bool HasMultipleHomeBatteries { get; set; }
    /// <summary>
    /// A home battery that seems to deliver more than was charged into it means the measurements are wrong, so neither
    /// learned value is used.
    /// </summary>
    public bool DischargingExceedsCharging { get; set; }
    /// <summary>The reliable learned usable energy of a car, null while there is none.</summary>
    public double? LearnedUsableEnergyKwh { get; set; }
    /// <summary>Reliable learned energy that charges the home battery from 0 to 100 %, null while there is none.</summary>
    public double? LearnedChargingEnergyKwh { get; set; }
    /// <summary>Reliable learned energy the home battery delivers from 100 to 0 %, null while there is none.</summary>
    public double? LearnedDischargingEnergyKwh { get; set; }
    /// <summary>Whether the learned values are used instead of the entered one.</summary>
    public bool IsUsed { get; set; }
    public List<DtoUsableEnergyEstimate> Estimates { get; set; } = new();
    /// <summary>The newest samples, accepted and rejected ones, newest first.</summary>
    public List<DtoUsableEnergySample> Samples { get; set; } = new();
}
