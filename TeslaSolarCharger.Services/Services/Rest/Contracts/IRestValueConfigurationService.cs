using System.Linq.Expressions;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;

namespace TeslaSolarCharger.Services.Services.Rest.Contracts;

public interface IRestValueConfigurationService
{
    Task<List<DtoRestValueConfiguration>> GetAllRestValueConfigurations();
    Task<List<DtoRestValueConfigurationHeader>> GetHeadersByConfigurationId(int parentId);
    Task<int> SaveHeader(int parentId, DtoRestValueConfigurationHeader dtoData);
    Task DeleteHeader(int id);
    Task<int> SaveRestValueConfiguration(DtoFullRestValueConfiguration dtoData);
    Task<List<DtoRestValueResultConfiguration>> GetResultConfigurationsByConfigurationId(int parentId);
    Task<int> SaveResultConfiguration(int parentId, DtoRestValueResultConfiguration dtoData);
    Task DeleteResultConfiguration(int id);
    Task<List<DtoFullRestValueConfiguration>> GetFullRestValueConfigurationsByPredicate(
        Expression<Func<RestValueConfiguration, bool>> predicate);

    Task<List<DtoRestValueResultConfiguration>> GetRestResultConfigurationByPredicate(
        Expression<Func<RestValueResultConfiguration, bool>> predicate);

    Task DeleteRestValueConfiguration(int id);
}
