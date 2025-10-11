namespace TeslaSolarCharger.Server.Services.Contracts;

public sealed record ManualCarOperationResult(bool IsManualCar, bool StateChanged)
{
    public static ManualCarOperationResult NotManual { get; } = new(false, false);
}
