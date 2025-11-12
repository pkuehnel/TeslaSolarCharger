using System.Linq.Expressions;
using TeslaSolarCharger.Model.Entities;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration;

namespace TeslaSolarCharger.Services.Services.Template.Contracts;

public interface ITemplateValueConfigurationService
{
    Task<ITemplateValueConfigurationDto?> GetAsync(int id);
    Task<TDto?> GetAsync<TDto>(int id) where TDto : class, ITemplateValueConfigurationDto;
    Task<IEnumerable<ITemplateValueConfigurationDto>> GetConfigurationsByPredicateAsync(Expression<Func<TemplateValueConfiguration, bool>> predicate);
    Task<int> SaveAsync<TDto>(TDto dto) where TDto : class, ITemplateValueConfigurationDto;
    Task DeleteAsync(int id);
}
