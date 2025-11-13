using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration;

namespace TeslaSolarCharger.Client.Services.Contracts;

public interface ITemplateValueConfigurationService
{
    Task<Result<DtoTemplateValueConfigurationBase>> GetAsync(int id);
    Task<List<DtoValueConfigurationOverview>> GetOverviews();
    Task<Result<int>> SaveAsync(DtoTemplateValueConfigurationBase configuration);
    Task<Result<object>> DeleteAsync(int id);
}
