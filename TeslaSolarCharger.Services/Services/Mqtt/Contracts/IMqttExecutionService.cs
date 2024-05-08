using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

namespace TeslaSolarCharger.Services.Services.Mqtt.Contracts;

public interface IMqttExecutionService
{
    Task<List<DtoValueConfigurationOverview>> GetMqttValueOverviews();
}
