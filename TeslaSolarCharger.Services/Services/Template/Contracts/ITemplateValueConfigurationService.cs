using System.Linq.Expressions;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration;

namespace TeslaSolarCharger.Services.Services.Template.Contracts;

public interface ITemplateValueConfigurationService
{
    Task<DtoTemplateValueConfigurationBase> GetAsync(int id);
    Task<IEnumerable<DtoTemplateValueConfigurationBase>> GetConfigurationsByPredicateAsync(Expression<Func<TemplateValueConfiguration, bool>> predicate);
    Task<int> SaveAsync<TDto>(TDto dto) where TDto : DtoTemplateValueConfigurationBase;
    Task DeleteAsync(int id);
}
