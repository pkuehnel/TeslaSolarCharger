using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Rest.Contracts;

public interface IValueOverviewService
{
    Task<List<DtoValueConfigurationOverview>> GetRestValueOverviews();
    Task<List<DtoValueConfigurationOverview>> GetMqttValueOverviews();
    Task<List<DtoValueConfigurationOverview>> GetModbusValueOverviews();
    Task<List<DtoValueConfigurationOverview>> GetTemplateValueOverviews();
}
