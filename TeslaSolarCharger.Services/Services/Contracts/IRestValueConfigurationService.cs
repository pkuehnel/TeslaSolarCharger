using System.Linq.Expressions;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Services.Services.Contracts;

public interface IRestValueConfigurationService
{
    Task<List<DtoRestValueConfiguration>> GetAllRestValueConfigurations();
    Task<List<DtoRestValueConfigurationHeader>> GetHeadersByConfigurationId(int parentId);
    Task<int> SaveHeader(int parentId, DtoRestValueConfigurationHeader dtoData);
    Task DeleteHeader(int id);
    Task<int> SaveRestValueConfiguration(DtoRestValueConfiguration dtoData);
    Task<List<DtoRestValueResultConfiguration>> GetResultConfigurationsByConfigurationId(int parentId);
    Task<int> SaveResultConfiguration(int parentId, DtoRestValueResultConfiguration dtoData);
    Task DeleteResultConfiguration(int id);
    Task<List<DtoFullRestValueConfiguration>> GetRestValueConfigurationsByPredicate(
        Expression<Func<RestValueConfiguration, bool>> predicate);

    Task<List<DtoRestValueResultConfiguration>> GetRestValueConfigurationsByPredicate(
        Expression<Func<RestValueResultConfiguration, bool>> predicate);
}
