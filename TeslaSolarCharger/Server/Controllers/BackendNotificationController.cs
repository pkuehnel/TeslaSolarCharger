using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class BackendNotificationController(IBackendNotificationService service) : ApiBaseController
{
    [HttpGet]
    public Task<List<DtoBackendNotification>> GetRelevantBackendNotifications()
    {
        return service.GetRelevantBackendNotifications();
    }

    [HttpPost]
    public Task MarkBackendNotificationAsConfirmed(int id)
    {
        return service.MarkBackendNotificationAsConfirmed(id);
    }
}
