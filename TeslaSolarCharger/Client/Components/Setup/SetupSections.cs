using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Client.Components.Setup;

/// <summary>
/// The addresses of the setup screens. A section is what appears in the URL and in the progress rail; the stable
/// <see cref="SetupStepKey"/> behind it is what gets persisted, so the two can be renamed independently.
/// </summary>
public static class SetupSections
{
    public const string Welcome = "welcome";
    public const string Account = "account";
    public const string Solar = "solar";
    public const string Location = "location";
    public const string Prices = "prices";
    public const string Equipment = "equipment";
    public const string Car = "car";
    public const string Charger = "charger";
    public const string Finish = "finish";

    /// <summary>
    /// The order the user walks through. The per device sections are detours off the equipment list rather than
    /// stops of their own, so they are not in here.
    /// </summary>
    public static readonly IReadOnlyList<string> Order = new[]
    {
        Welcome, Account, Solar, Location, Prices, Equipment, Finish,
    };

    public static SetupStepKey ToStepKey(string? section) => section switch
    {
        Welcome => SetupStepKey.Welcome,
        Account => SetupStepKey.CloudConnection,
        Solar => SetupStepKey.SolarAndBattery,
        Location => SetupStepKey.Location,
        Prices => SetupStepKey.Prices,
        //Setting up a car or a charger is part of describing the equipment, so all three share one stored step.
        Equipment or Car or Charger => SetupStepKey.CarsAndCharging,
        Finish => SetupStepKey.Finish,
        _ => SetupStepKey.Unknown,
    };

    public static string ToSection(SetupStepKey stepKey) => stepKey switch
    {
        SetupStepKey.Welcome => Welcome,
        SetupStepKey.CloudConnection => Account,
        SetupStepKey.SolarAndBattery => Solar,
        SetupStepKey.Location => Location,
        SetupStepKey.Prices => Prices,
        SetupStepKey.CarsAndCharging => Equipment,
        SetupStepKey.Finish => Finish,
        //A step this build does not know must not leave the user on a screen that is not there.
        _ => Welcome,
    };

    public static string? Next(string section)
    {
        var index = Order.ToList().IndexOf(section);
        return index >= 0 && index < Order.Count - 1 ? Order[index + 1] : null;
    }

    public static string? Previous(string section)
    {
        var index = Order.ToList().IndexOf(section);
        return index > 0 ? Order[index - 1] : null;
    }

    /// <summary>A per device detour returns to the equipment list rather than to the section before it.</summary>
    public static bool IsDeviceSection(string? section) => section is Car or Charger;
}
