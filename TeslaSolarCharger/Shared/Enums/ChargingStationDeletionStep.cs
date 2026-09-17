namespace TeslaSolarCharger.Shared.Enums;

/// <summary>
/// Describes which group of charging station related data is currently being deleted so the UI can show
/// meaningful progress.
/// </summary>
public enum ChargingStationDeletionStep
{
    ChargingProcesses,
    Transactions,
    ConnectorValueLogs,
    MeterValues,
    ConnectorAssignments,
    Connectors,
    ChargingStation,
}
