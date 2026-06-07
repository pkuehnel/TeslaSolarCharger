namespace TeslaSolarCharger.Client.Dialogs;

/// <summary>
/// Tells the Car Settings page what to do after the add-car wizard closes.
/// </summary>
public enum AddCarResultAction
{
    /// <summary>The user picked "manual car"; the page should open the car edit dialog for a new manual car.</summary>
    EditNewManualCar,

    /// <summary>Cars were added/changed server-side (e.g. Tesla discovery); the page should just refresh its list.</summary>
    Refresh,
}
