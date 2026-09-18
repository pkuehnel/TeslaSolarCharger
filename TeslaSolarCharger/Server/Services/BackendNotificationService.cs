using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Helper.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class BackendNotificationService (ILogger<BackendNotificationService> logger,
    ITeslaSolarChargerContext context,
    IDateTimeProvider dateTimeProvider,
    IBackendApiService backendApiService,
    IVersionHelper versionHelper) : IBackendNotificationService
{
    public async Task<List<DtoBackendNotification>> GetRelevantBackendNotifications()
    {
        logger.LogTrace("{method}()", nameof(GetRelevantBackendNotifications));
        var currentDate = dateTimeProvider.UtcNow();
        var versionString = await backendApiService.GetCurrentVersion();
        var notAcknoledgedDbNotifications = await context.BackendNotifications
            .Where(n => !n.IsConfirmed)
            .AsNoTracking()
            .ToListAsync().ConfigureAwait(false);
        var backendNotifications = new List<DtoBackendNotification>();
        foreach (var dbNotification in notAcknoledgedDbNotifications)
        {
            if (dbNotification.ValidFromDate > currentDate)
            {
                continue;
            }
            if (dbNotification.ValidToDate < currentDate)
            {
                continue;
            }
            if (!versionHelper.IsVersionInRange(versionString, dbNotification.ValidFromVersion, dbNotification.ValidToVersion))
            {
                continue;
            }
            backendNotifications.Add(new DtoBackendNotification
            {
                Id = dbNotification.Id,
                Type = dbNotification.Type,
                Headline = dbNotification.Headline,
                DetailText = dbNotification.DetailText,
                ValidFromDate = dbNotification.ValidFromDate,
                ValidToDate = dbNotification.ValidToDate,
                ValidFromVersion = dbNotification.ValidFromVersion,
                ValidToVersion = dbNotification.ValidToVersion,
            });
        }

        return backendNotifications;
    }

    public async Task MarkBackendNotificationAsConfirmed(int id)
    {
        logger.LogTrace("{method}({id})", nameof(MarkBackendNotificationAsConfirmed), id);
        var notification = await context.BackendNotifications.FindAsync(id).ConfigureAwait(false);
        if (notification == null)
        {
            throw new ArgumentException("Notification not found.");
        }
        notification.IsConfirmed = true;
        await context.SaveChangesAsync().ConfigureAwait(false);
    }
}
