namespace TeslaSolarCharger.Shared.Enums;

public enum UsableEnergySampleRejectionReason
{
    /// <summary>Fewer than two real (not estimated) SoC readings during the charge.</summary>
    MissingSoc,
    /// <summary>The SoC changed too little for the integer SoC to give a precise result.</summary>
    SocDifferenceTooSmall,
    /// <summary>No energy counter value before and after one of the SoC readings.</summary>
    MissingMeterValues,
    /// <summary>The energy values have a gap, so part of the energy is unknown.</summary>
    MeterValueGap,
    /// <summary>The SoC jumped without matching energy, e.g. a recalibration of the battery management.</summary>
    SocJump,
    /// <summary>Too much energy flowed in the opposite direction during the run.</summary>
    OppositeEnergyFlow,
    /// <summary>The capacity this sample implies is outside of any realistic battery size.</summary>
    ImplausibleCapacity,
}
