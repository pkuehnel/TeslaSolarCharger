namespace TeslaSolarCharger.Shared.Helper;

/// <summary>
/// Decides which usable energy a battery is planned with: the value learned from past charges while learning is
/// enabled and a reliable value exists, otherwise the value the user entered.
/// </summary>
public static class UsableEnergySelector
{
    public static int? SelectWh(bool useLearned, int? learnedWh, int? enteredWh)
    {
        return useLearned && learnedWh > 0 ? learnedWh : enteredWh;
    }

    /// <summary>
    /// Car usable energies are entered and planned in whole kWh, so a learned value is rounded to the nearest kWh.
    /// </summary>
    public static int SelectCarKwh(bool useLearned, IReadOnlyDictionary<int, int>? learnedWhByCarId, int carId, int enteredKwh)
    {
        var learnedWh = learnedWhByCarId != null && learnedWhByCarId.TryGetValue(carId, out var value) ? value : (int?)null;
        var selectedWh = SelectWh(useLearned, learnedWh, enteredKwh * 1000);
        return (int)Math.Round(selectedWh!.Value / 1000.0, MidpointRounding.AwayFromZero);
    }
}
