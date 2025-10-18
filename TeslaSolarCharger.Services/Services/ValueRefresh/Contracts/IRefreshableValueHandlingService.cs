using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;

public interface IRefreshableValueHandlingService
{
    IReadOnlyDictionary<ValueUsage, decimal> GetSolarValues(out bool hasErrors);
    Task RecreateRefreshables();
    Task RefreshValues();
}
