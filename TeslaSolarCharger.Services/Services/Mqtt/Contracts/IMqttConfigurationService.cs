using System.Linq.Expressions;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.Shared.Dtos.MqttConfiguration;

namespace TeslaSolarCharger.Services.Services.Mqtt.Contracts;

public interface IMqttConfigurationService
{
    Task<List<DtoMqttConfiguration>> GetMqttConfigurationsByPredicate(Expression<Func<MqttConfiguration, bool>> predicate);
    Task<int> SaveConfiguration(DtoMqttConfiguration dtoData);
    Task DeleteConfiguration(int id);
    Task<DtoMqttConfiguration> GetConfigurationById(int id);
    Task<List<DtoMqttResultConfiguration>> GetMqttResultConfigurationsByPredicate(Expression<Func<MqttResultConfiguration, bool>> predicate);
    Task<DtoMqttResultConfiguration> GetResultConfigurationById(int id);
    Task<int> SaveResultConfiguration(int parentId, DtoMqttResultConfiguration dtoData);
    Task DeleteResultConfiguration(int id);
}
