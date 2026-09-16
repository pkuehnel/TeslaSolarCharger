using TeslaSolarCharger.Server.Services.SolarValueGathering.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Fake.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Fake;

/// <summary>
/// Holds the made-up solar values as a device of their own, so they reach the charging logic and the pages the same
/// way the values of a real device do. The device stays empty while fake solar values are switched off.
/// </summary>
public class FakeSolarValueHandlingService : DecimalValueHandlingServiceBase<FakeSolarValue>, IFakeSolarValueHandlingService
{
    private readonly FakeSolarValue _fakeSolarValue;

    public FakeSolarValueHandlingService(IServiceScopeFactory serviceScopeFactory) : base(serviceScopeFactory)
    {
        _fakeSolarValue = new FakeSolarValue(serviceScopeFactory);
        AddGenericValues([_fakeSolarValue,]);
    }

    /// <summary>The fake device is not configured anywhere, so there is nothing to recreate it from.</summary>
    public override Task RecreateValues(ConfigurationType? configurationType, params List<int> configurationIds) => Task.CompletedTask;

    public void SetValues(DateTimeOffset timestamp, IReadOnlyDictionary<ValueUsage, int?> values) =>
        _fakeSolarValue.SetValues(timestamp, values);
}
