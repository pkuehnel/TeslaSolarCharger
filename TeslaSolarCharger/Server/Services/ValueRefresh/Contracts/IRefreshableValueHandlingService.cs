using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.ValueRefresh.Contracts;

public interface IRefreshableValueHandlingService
{
    IReadOnlyDictionary<ValueUsage, decimal> GetSolarValues();
    Task RecreateRefreshables();
    Task RefreshValues();
}
