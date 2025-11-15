using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;

public interface IRefreshableValueHandlingService
{
    IReadOnlyDictionary<ValueUsage, List<DtoHistoricValue<decimal>>> GetSolarValues(out bool hasErrors);
    Task RecreateRefreshables(ConfigurationType? configurationType, params List<int> configurationIds);
    Task RefreshValues();
}
