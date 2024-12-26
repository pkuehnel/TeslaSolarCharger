using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using System.Globalization;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class TeslaFleetApiTokenHelper(ILogger<TeslaFleetApiTokenHelper> logger,
    ISettings settings,
    IConfigurationWrapper configurationWrapper,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    IConstants constants,
    IDateTimeProvider dateTimeProvider) : ITeslaFleetApiTokenHelper
{
    public async Task<FleetApiTokenState> GetFleetApiTokenState()
    {
        logger.LogTrace("{method}()", nameof(GetFleetApiTokenState));
        if (!settings.AllowUnlimitedFleetApiRequests)
        {
            return FleetApiTokenState.NoApiRequestsAllowed;
        }
        var hasCurrentTokenMissingScopes = await teslaSolarChargerContext.TscConfigurations
            .Where(c => c.Key == constants.TokenMissingScopes)
            .AnyAsync().ConfigureAwait(false);
        if (hasCurrentTokenMissingScopes)
        {
            return FleetApiTokenState.MissingScopes;
        }
        var token = await teslaSolarChargerContext.BackendTokens.FirstOrDefaultAsync().ConfigureAwait(false);
        if (token != null)
        {
            if (token.UnauthorizedCounter > constants.MaxTokenUnauthorizedCount)
            {
                return FleetApiTokenState.TokenUnauthorized;
            }
            return (token.ExpiresAtUtc < dateTimeProvider.UtcNow() ? FleetApiTokenState.Expired : FleetApiTokenState.UpToDate);
        }
        var tokenRequestedDate = await GetTokenRequestedDate().ConfigureAwait(false);
        if (tokenRequestedDate == null)
        {
            return FleetApiTokenState.NotRequested;
        }
        var currentDate = dateTimeProvider.UtcNow();
        if (tokenRequestedDate < (currentDate - constants.MaxTokenRequestWaitTime))
        {
            return FleetApiTokenState.TokenRequestExpired;
        }
        return FleetApiTokenState.NotReceived;
    }

    public async Task<DateTime?> GetTokenRequestedDate()
    {
        logger.LogTrace("{method}()", nameof(GetTokenRequestedDate));
        var tokenRequestedDateString = await teslaSolarChargerContext.TscConfigurations
            .Where(c => c.Key == constants.FleetApiTokenRequested)
            .Select(c => c.Value)
            .FirstOrDefaultAsync().ConfigureAwait(false);
        if (tokenRequestedDateString == null)
        {
            return null;
        }
        var tokenRequestedDate = DateTime.Parse(tokenRequestedDateString, null, DateTimeStyles.RoundtripKind);
        return tokenRequestedDate;
    }
}
