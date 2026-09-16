using TeslaSolarCharger.Server.Services.SolarValueGathering.Contracts;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Fake.Contracts;

public interface IFakeSolarValueHandlingService : IDecimalValueHandlingService
{
    /// <inheritdoc cref="FakeSolarValue.SetValues"/>
    void SetValues(DateTimeOffset timestamp, IReadOnlyDictionary<ValueUsage, int?> values);
}
