using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Server.Services;

public class HomeService : IHomeService
{
    private readonly ILogger<HomeService> _logger;
    private readonly ITeslaSolarChargerContext _context;
    private readonly ILoadPointManagementService _loadPointManagementService;

    public HomeService(ILogger<HomeService> logger,
        ITeslaSolarChargerContext context,
        ILoadPointManagementService loadPointManagementService)
    {
        _logger = logger;
        _context = context;
        _loadPointManagementService = loadPointManagementService;
    }


    public async Task<List<DtoLoadPointOverview>> GetPluggedInLoadPoints()
    {
        _logger.LogTrace("{method}()", nameof(GetPluggedInLoadPoints));
        var rawLoadPoints = await _loadPointManagementService.GetPluggedInLoadPoints();
        var result = new List<DtoLoadPointOverview>();
        foreach (var dtoLoadpoint in rawLoadPoints)
        {
            var loadPointOverview = new DtoLoadPointOverview();
            if (dtoLoadpoint.Car != default)
            {
                loadPointOverview.CarId = dtoLoadpoint.Car.Id;
                loadPointOverview.CarName = dtoLoadpoint.Car.Name ?? dtoLoadpoint.Car.Vin;
                loadPointOverview.ChargingPhaseCount = dtoLoadpoint.Car.ActualPhases;
                loadPointOverview.MaxCurrent = dtoLoadpoint.Car.MaximumAmpere;
                loadPointOverview.ChargingCurrent = dtoLoadpoint.Car.ChargerActualCurrent ?? 0;
            }
            if (dtoLoadpoint.OcppConnectorId != default)
            {
                loadPointOverview.ChargingConnectorId = dtoLoadpoint.OcppConnectorId;
                var relevantConnectorValues = await _context.OcppChargingStationConnectors
                    .Where(c => c.Id == dtoLoadpoint.OcppConnectorId)
                    .Select(c => new
                    {
                        c.Name,
                        c.ConnectedPhasesCount,
                        c.MaxCurrent,
                    })
                    .FirstAsync().ConfigureAwait(false);
                loadPointOverview.ChargingConnectorName = relevantConnectorValues.Name;
                loadPointOverview.MaxPhaseCount = relevantConnectorValues.ConnectedPhasesCount;
                //OcppConnectorState can not be null if OcppConnectorId is not null.
                if (dtoLoadpoint.OcppConnectorState!.PhaseCount.Value != default)
                {
                    loadPointOverview.ChargingPhaseCount = dtoLoadpoint.OcppConnectorState.PhaseCount.Value;
                }
                if (relevantConnectorValues.MaxCurrent < loadPointOverview.MaxCurrent)
                {
                    loadPointOverview.MaxCurrent = relevantConnectorValues.MaxCurrent.Value;
                }
            }
            loadPointOverview.ChargingPower = dtoLoadpoint.ActualChargingPower ?? 0;
            loadPointOverview.ChargingCurrent = dtoLoadpoint.ActualCurrent ?? 0;
            result.Add(loadPointOverview);
        }
        return result;
    }
}
