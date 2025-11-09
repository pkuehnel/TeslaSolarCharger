using TeslaSolarCharger.Services.Services.ValueRefresh;

namespace TeslaSolarCharger.Services.Services.Rest.Contracts;

public interface IRefreshableValueSetupService
{
    Task<List<DelegateRefreshableValue<decimal>>> GetDecimalRefreshableValuesAsync(TimeSpan defaultInterval);
}
