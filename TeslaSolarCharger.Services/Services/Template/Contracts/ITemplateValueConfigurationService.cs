using System.Linq.Expressions;
using TeslaSolarCharger.Model.Entities;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

namespace TeslaSolarCharger.Services.Services.Template.Contracts;

public interface ITemplateValueConfigurationService
{
    Task<TDto?> GetAsync<TDto>(int id) where TDto : class;
    Task<IEnumerable<object>> GetAllAsync(Expression<Func<TemplateValueConfiguration, bool>> predicate);
    Task<int> SaveAsync<TDto>(TDto dto) where TDto : class;
    Task DeleteAsync(int id);
}
