using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Rest.Contracts;

public interface IAutoRefreshingValueSetupService
{
    ConfigurationType ConfigurationType { get; }
    Task<List<IAutoRefreshingValue<decimal>>> GetDecimalAutoRefreshingValuesAsync(List<int> configurationIds);
}
