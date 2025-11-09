using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration;

namespace TeslaSolarCharger.Services.Services.Template.Infrastructure.Contracts;

public interface ITemplateValueConfigurationFactory
{
    ITemplateValueConfigurationDto CreateDto(TemplateValueConfiguration entity);
    TemplateValueConfiguration CreateEntity(ITemplateValueConfigurationDto dto);
}
