using TeslaSolarCharger.Services.Services.ValueRefresh;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;

namespace TeslaSolarCharger.Services.Services.Rest.Contracts;

public interface IAutoRefreshingValueSetupService
{
    ConfigurationType ConfigurationType { get; }
    Task<List<IAutoRefreshingValue<decimal>>> GetDecimalAutoRefreshingValuesAsync(List<int> configurationIds);
}
