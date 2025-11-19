using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Rest.Contracts;

public interface IRefreshableValueSetupService
{
    ConfigurationType ConfigurationType { get; }
    Task<List<DelegateRefreshableValue<decimal>>> GetDecimalRefreshableValuesAsync(TimeSpan defaultInterval, List<int> configurationIds);
}
