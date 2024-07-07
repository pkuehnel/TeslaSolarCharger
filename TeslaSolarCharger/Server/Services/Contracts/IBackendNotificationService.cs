using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IBackendNotificationService
{
    Task<List<DtoBackendNotification>> GetRelevantBackendNotifications();
    Task MarkBackendNotificationAsConfirmed(int id);
}
