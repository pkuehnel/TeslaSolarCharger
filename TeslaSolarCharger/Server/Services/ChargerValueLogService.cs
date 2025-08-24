using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class ChargerValueLogService : IChargerValueLogService
{
    private readonly ILogger<ChargerValueLogService> _logger;
    private readonly ITeslaSolarChargerContext _context;
    private readonly IDatabaseValueBufferService _databaseValueBufferService;

    public ChargerValueLogService(ILogger<ChargerValueLogService> logger,
        ITeslaSolarChargerContext context,
        IDatabaseValueBufferService databaseValueBufferService)
    {
        _logger = logger;
        _context = context;
        _databaseValueBufferService = databaseValueBufferService;
    }

    public async Task SaveBufferedChargerValuesToDatabase()
    {
        _logger.LogTrace("{method}()", nameof(SaveBufferedChargerValuesToDatabase));
        var values = _databaseValueBufferService.DrainAll<OcppChargingStationConnectorValueLog>();
        _logger.LogTrace("Drained {count} buffered charger log values", values.Count);
        _context.OcppChargingStationConnectorValueLogs.AddRange(values);
        await _context.SaveChangesAsync().ConfigureAwait(false);
    }
}
