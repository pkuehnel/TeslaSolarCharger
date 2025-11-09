using TeslaSolarCharger.Model.Entities;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration;

namespace TeslaSolarCharger.Services.Services.Template.Infrastructure.Contracts;

public interface ITemplateValueConfigurationFactory
{
    object CreateDto(TemplateValueConfiguration entity);
    TemplateValueConfiguration CreateEntity(object dto);
}
