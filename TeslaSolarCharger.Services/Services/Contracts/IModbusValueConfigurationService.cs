using System.Linq.Expressions;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;

namespace TeslaSolarCharger.Services.Services.Contracts;

public interface IModbusValueConfigurationService
{
    Task<List<DtoModbusConfiguration>> GetModbusConfigurationByPredicate(Expression<Func<ModbusConfiguration, bool>> predicate);
    Task<int> SaveModbusConfiguration(DtoModbusConfiguration dtoData);

    Task<List<DtoModbusValueResultConfiguration>> GetModbusResultConfigurationsByPredicate(
        Expression<Func<ModbusResultConfiguration, bool>> predicate);

    Task<int> SaveModbusResultConfiguration(DtoModbusValueResultConfiguration dtoData);
}
