using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.Infrastructure.Contracts;

public interface ITemplateValueConfigurationFactory
{
    DtoTemplateValueConfigurationBase CreateDto(TemplateValueConfiguration entity);
    TemplateValueConfiguration CreateEntity(DtoTemplateValueConfigurationBase dtoBase);
}
