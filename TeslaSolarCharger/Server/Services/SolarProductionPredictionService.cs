using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class SolarProductionPredictionService(ILogger<SolarProductionPredictionService> logger,
    ITeslaSolarChargerContext context) : ISolarProductionPredictionService
{
    public async Task<Dictionary<int, int>> GetPredictedProductionByLocalHour(DateOnly date)
    {
        logger.LogTrace("{method}({date})", nameof(GetPredictedProductionByLocalHour), date);
        var localStart = date.ToDateTime(TimeOnly.MinValue);
        var localStartOffset = new DateTimeOffset(localStart, TimeZoneInfo.Local.GetUtcOffset(localStart));
        var utcStart = localStartOffset.ToUniversalTime();
        var utcEnd = utcStart.AddDays(1);
        var latestRadiations = await context.SolarRadiations
            .Where(r => r.Start >= utcStart && r.End <= utcEnd)
            .GroupBy(r => new { r.Start, r.End })
            .Select(g => g.OrderByDescending(r => r.CreatedAt).First())
            .ToListAsync();
        latestRadiations = latestRadiations.OrderBy(r => r.Start).ToList();
        throw new NotImplementedException();
        return new Dictionary<int, int>();
    }
}
