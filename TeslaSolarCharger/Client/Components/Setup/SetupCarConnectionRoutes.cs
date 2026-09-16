using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Client.Components.Setup;

/// <summary>
/// What each way of reaching a car is called, what it needs and whether it costs extra. Kept in one place because the
/// setup assistant, the equipment list and the car settings page all describe the same routes, and two descriptions
/// of one route that drift apart would tell the user two different things.
/// </summary>
public static class SetupCarConnectionRoutes
{
    /// <summary>The routes a Tesla can take: it is reached directly, so charging is controlled through the car.</summary>
    public static readonly IReadOnlyList<SetupCarConnectionRoute> TeslaRoutes =
    [
        SetupCarConnectionRoute.TeslaBluetooth,
        SetupCarConnectionRoute.TeslaCloud,
    ];

    /// <summary>The routes any other car can take: charging is controlled by the charging station it is plugged into.</summary>
    public static readonly IReadOnlyList<SetupCarConnectionRoute> OtherCarRoutes =
    [
        SetupCarConnectionRoute.SmartCarWithChargingStation,
        SetupCarConnectionRoute.ChargingStationOnly,
    ];

    /// <summary>Whether the route is one a car can actually take, rather than no answer or one this build does not know.</summary>
    public static bool IsDecided(SetupCarConnectionRoute route) =>
        TeslaRoutes.Contains(route) || OtherCarRoutes.Contains(route);

    public static string NameKey(SetupCarConnectionRoute route) => route switch
    {
        SetupCarConnectionRoute.TeslaBluetooth => TranslationKeys.SetupRouteNameTeslaBluetooth,
        SetupCarConnectionRoute.TeslaCloud => TranslationKeys.SetupRouteNameTeslaCloud,
        SetupCarConnectionRoute.SmartCarWithChargingStation => TranslationKeys.SetupRouteNameSmartCar,
        SetupCarConnectionRoute.ChargingStationOnly => TranslationKeys.SetupRouteNameChargingStation,
        _ => TranslationKeys.SetupRouteNotDecidedYet,
    };

    public static string? DescriptionKey(SetupCarConnectionRoute route) => route switch
    {
        SetupCarConnectionRoute.TeslaBluetooth => TranslationKeys.SetupRouteDescriptionTeslaBluetooth,
        SetupCarConnectionRoute.TeslaCloud => TranslationKeys.SetupRouteDescriptionTeslaCloud,
        SetupCarConnectionRoute.SmartCarWithChargingStation => TranslationKeys.SetupRouteDescriptionSmartCar,
        SetupCarConnectionRoute.ChargingStationOnly => TranslationKeys.SetupRouteDescriptionChargingStation,
        _ => null,
    };

    public static string? RequirementKey(SetupCarConnectionRoute route) => route switch
    {
        SetupCarConnectionRoute.TeslaBluetooth => TranslationKeys.SetupRouteRequirementTeslaBluetooth,
        SetupCarConnectionRoute.TeslaCloud => TranslationKeys.SetupRouteRequirementTeslaCloud,
        SetupCarConnectionRoute.SmartCarWithChargingStation => TranslationKeys.SetupRouteRequirementSmartCar,
        SetupCarConnectionRoute.ChargingStationOnly => TranslationKeys.SetupRouteRequirementChargingStation,
        _ => null,
    };

    /// <summary>
    /// Whether the route needs a subscription for the car on top of the base licence. An undecided route costs
    /// nothing yet, so it is not reported as paid.
    /// </summary>
    public static bool RequiresSubscription(SetupCarConnectionRoute route) =>
        route is SetupCarConnectionRoute.TeslaCloud or SetupCarConnectionRoute.SmartCarWithChargingStation;
}
