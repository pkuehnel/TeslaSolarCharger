using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
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


    public async Task<List<DtoLoadPointOverview>> GetLoadPointOverviews()
    {
        _logger.LogTrace("{method}()", nameof(GetLoadPointOverviews));
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
                loadPointOverview.Soc = dtoLoadpoint.Car.SoC;
                loadPointOverview.CarSideSocLimit = dtoLoadpoint.Car.SocLimit;
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
                if (relevantConnectorValues.MaxCurrent < loadPointOverview.MaxCurrent || dtoLoadpoint.Car == default)
                {
                    loadPointOverview.MaxCurrent = relevantConnectorValues.MaxCurrent;
                }
            }
            loadPointOverview.ChargingPower = dtoLoadpoint.ActualChargingPower ?? 0;
            loadPointOverview.ChargingCurrent = dtoLoadpoint.ActualCurrent ?? 0;
            result.Add(loadPointOverview);
        }
        return result;
    }

    public async Task<List<DtoCarChargingSchedule>> GetCarChargingSchedules(int carId)
    {
        _logger.LogTrace("{method}({carId})", nameof(GetCarChargingSchedules), carId);
        var chargingSchedules = await _context.CarChargingSchedules
            .Where(s => s.CarId == carId)
            .Select(s => new DtoCarChargingSchedule()
            {
                Id = s.Id,
                TargetSoc = s.TargetSoc,
                NextOccurrence = s.NextOccurrence,
                RepeatOnMondays = s.RepeatOnMondays,
                RepeatOnTuesdays = s.RepeatOnTuesdays,
                RepeatOnWednesdays = s.RepeatOnWednesdays,
                RepeatOnThursdays = s.RepeatOnThursdays,
                RepeatOnFridays = s.RepeatOnFridays,
                RepeatOnSaturdays = s.RepeatOnSaturdays,
                RepeatOnSundays = s.RepeatOnSundays,
            })
            .ToListAsync().ConfigureAwait(false);
        return chargingSchedules;
    }

    public async Task<Result<int>> SaveCarChargingSchedule(int carId, DtoCarChargingSchedule dto)
    {
        _logger.LogTrace("{method}({carId}, {@chargingSchedule})", nameof(SaveCarChargingSchedule), carId, dto);
        var dbValue = await _context.CarChargingSchedules
            .FirstOrDefaultAsync(s => s.Id == dto.Id).ConfigureAwait(false);
        if (dbValue == null)
        {
            dbValue = new();
            _context.CarChargingSchedules.Add(dbValue);
        }

        dbValue.CarId = carId;
        dbValue.TargetSoc = dto.TargetSoc;
        //Next occurrence can not be null as is validated
        dbValue.NextOccurrence = dto.NextOccurrence!.Value;
        dbValue.RepeatOnMondays = dto.RepeatOnMondays;
        dbValue.RepeatOnTuesdays = dto.RepeatOnTuesdays;
        dbValue.RepeatOnWednesdays = dto.RepeatOnWednesdays;
        dbValue.RepeatOnThursdays = dto.RepeatOnThursdays;
        dbValue.RepeatOnFridays = dto.RepeatOnFridays;
        dbValue.RepeatOnSaturdays = dto.RepeatOnSaturdays;
        dbValue.RepeatOnSundays = dto.RepeatOnSundays;
        await _context.SaveChangesAsync();
        return new(dbValue.Id, null, null);
    }
}
