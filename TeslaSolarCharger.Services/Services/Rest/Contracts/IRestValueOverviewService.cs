using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

namespace TeslaSolarCharger.Services.Services.Rest.Contracts;

public interface IRestValueOverviewService
{
    Task<List<DtoValueConfigurationOverview>> GetRestValueOverviews();
}
