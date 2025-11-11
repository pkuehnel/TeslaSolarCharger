using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

namespace TeslaSolarCharger.Services.Services.Rest.Contracts;

public interface IValueOverviewService
{
    Task<List<DtoValueConfigurationOverview>> GetRestValueOverviews();
    Task<List<DtoValueConfigurationOverview>> GetMqttValueOverviews();
    Task<List<DtoValueConfigurationOverview>> GetModbusValueOverviews();
}
