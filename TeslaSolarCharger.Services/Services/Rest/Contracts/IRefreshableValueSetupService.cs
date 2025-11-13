using TeslaSolarCharger.Services.Services.ValueRefresh;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;

namespace TeslaSolarCharger.Services.Services.Rest.Contracts;

public interface IRefreshableValueSetupService
{
    ConfigurationType ConfigurationType { get; }
    Task<List<DelegateRefreshableValue<decimal>>> GetDecimalRefreshableValuesAsync(TimeSpan defaultInterval, List<int> configurationIds);
}
