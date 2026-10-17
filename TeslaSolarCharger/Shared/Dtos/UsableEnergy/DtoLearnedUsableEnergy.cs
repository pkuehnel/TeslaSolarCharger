namespace TeslaSolarCharger.Shared.Dtos.UsableEnergy;

/// <summary>Learning state of one battery, shown next to the entered usable energy.</summary>
public class DtoLearnedUsableEnergy
{
    public bool LearningEnabled { get; set; }
    /// <summary>The summed SoC of several home batteries does not describe one battery, so nothing is learned.</summary>
    public bool HasMultipleHomeBatteries { get; set; }
    /// <summary>The reliable learned usable energy, null while there is none.</summary>
    public double? LearnedUsableEnergyKwh { get; set; }
    /// <summary>Whether the learned value is used instead of the entered one.</summary>
    public bool IsUsed { get; set; }
    public List<DtoUsableEnergyEstimate> Estimates { get; set; } = new();
    /// <summary>The newest samples, accepted and rejected ones, newest first.</summary>
    public List<DtoUsableEnergySample> Samples { get; set; } = new();
}
