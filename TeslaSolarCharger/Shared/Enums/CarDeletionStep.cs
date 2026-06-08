namespace TeslaSolarCharger.Shared.Enums;

/// <summary>
/// Describes which group of car related data is currently being deleted so the UI can show meaningful progress.
/// </summary>
public enum CarDeletionStep
{
    ChargingProcesses,
    HandledCharges,
    CarValueLogs,
    MeterValues,
    ChargingTargets,
    ConnectorAssignments,
    Car,
}
