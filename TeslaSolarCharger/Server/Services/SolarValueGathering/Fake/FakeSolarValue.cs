using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Fake;

/// <summary>
/// The device the made-up solar values come from when fake solar values are switched on. It is never configured, so it
/// has no refresh of its own: whoever makes up the values sets them.
/// </summary>
public sealed class FakeSolarValue(IServiceScopeFactory serviceScopeFactory) : GenericValueBase<decimal>(serviceScopeFactory, 1)
{
    public override SourceValueKey SourceValueKey { get; } = new(0, ConfigurationType.FakeSolarValue);

    /// <summary>
    /// Replaces the values of every measurement in <paramref name="values"/>. A null value removes that measurement, as
    /// if the device did not supply it.
    /// </summary>
    public void SetValues(DateTimeOffset timestamp, IReadOnlyDictionary<ValueUsage, int?> values)
    {
        foreach (var (usage, value) in values)
        {
            var valueKey = new ValueKey(usage, null, 0);
            if (value == null)
            {
                RemoveValue(valueKey);
                continue;
            }

            UpdateValue(valueKey, timestamp, value.Value);
        }
    }

    public override void Cancel()
    {
    }

    public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
