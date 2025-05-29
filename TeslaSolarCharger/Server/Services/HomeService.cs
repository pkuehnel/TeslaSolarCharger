using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Server.Services;

public class HomeService : IHomeService
{
    private readonly ILogger<HomeService> _logger;
    private readonly ITeslaSolarChargerContext _context;
    private readonly ILoadPointManagementService _loadPointManagementService;
    private readonly ISettings _settings;

    public HomeService(ILogger<HomeService> logger,
        ITeslaSolarChargerContext context,
        ILoadPointManagementService loadPointManagementService,
        ISettings settings)
    {
        _logger = logger;
        _context = context;
        _loadPointManagementService = loadPointManagementService;
        _settings = settings;
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
                loadPointOverview.MinSoc = dtoLoadpoint.Car.MinimumSoC;
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

    public async Task<DtoCarChargingSchedule> GetChargingSchedule(int chargingScheduleId)
    {
        _logger.LogTrace("{method}({chargingScheduleId})", nameof(GetChargingSchedule), chargingScheduleId);
        return await _context.CarChargingSchedules
            .Where(s => s.Id == chargingScheduleId)
            .Select(ToDto)
            .FirstAsync()
            .ConfigureAwait(false);
    }

    public async Task<List<DtoCarChargingSchedule>> GetCarChargingSchedules(int carId)
    {
        _logger.LogTrace("{method}({carId})", nameof(GetCarChargingSchedules), carId);
        return await _context.CarChargingSchedules
            .Where(s => s.CarId == carId)
            .Select(ToDto)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    private static readonly Expression<Func<CarChargingSchedule, DtoCarChargingSchedule>> ToDto =
        s => new DtoCarChargingSchedule
        {
            Id = s.Id,
            TargetSoc = s.TargetSoc,
            TargetDate = s.TargetDate.HasValue
                ? DateTime.SpecifyKind(
                    s.TargetDate.Value.ToDateTime(TimeOnly.MinValue),
                    DateTimeKind.Local)
                : null,
            TargetTime = s.TargetTime.ToTimeSpan(),
            RepeatOnMondays = s.RepeatOnMondays,
            RepeatOnTuesdays = s.RepeatOnTuesdays,
            RepeatOnWednesdays = s.RepeatOnWednesdays,
            RepeatOnThursdays = s.RepeatOnThursdays,
            RepeatOnFridays = s.RepeatOnFridays,
            RepeatOnSaturdays = s.RepeatOnSaturdays,
            RepeatOnSundays = s.RepeatOnSundays,
            ClientTimeZone = s.ClientTimeZone,
        };

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
        dbValue.TargetDate = dto.TargetDate == default ? null : DateOnly.FromDateTime(dto.TargetDate.Value);
        //Target Time can not be null due to validation
        dbValue.TargetTime = TimeOnly.FromTimeSpan(dto.TargetTime!.Value);
        dbValue.RepeatOnMondays = dto.RepeatOnMondays;
        dbValue.RepeatOnTuesdays = dto.RepeatOnTuesdays;
        dbValue.RepeatOnWednesdays = dto.RepeatOnWednesdays;
        dbValue.RepeatOnThursdays = dto.RepeatOnThursdays;
        dbValue.RepeatOnFridays = dto.RepeatOnFridays;
        dbValue.RepeatOnSaturdays = dto.RepeatOnSaturdays;
        dbValue.RepeatOnSundays = dto.RepeatOnSundays;
        dbValue.ClientTimeZone = dto.ClientTimeZone;
        await _context.SaveChangesAsync();
        return new(dbValue.Id, null, null);
    }

    public async Task DeleteChargingSchedule(int chargingScheduleId)
    {
        _logger.LogTrace("{method}({chargingScheduleId})", nameof(DeleteChargingSchedule), chargingScheduleId);
        _context.CarChargingSchedules.Remove(new() { Id = chargingScheduleId });
        await _context.SaveChangesAsync();
    }

    public async Task UpdateCarMinSoc(int carId, int newMinSoc)
    {
        _logger.LogTrace("{method}({carId}, {minSoc})", nameof(UpdateCarMinSoc), carId, newMinSoc);
        var dbCar = await _context.Cars.FirstAsync(c => c.Id == carId).ConfigureAwait(false);
        dbCar.MinimumSoc = newMinSoc;
        await _context.SaveChangesAsync();
        var dtoCar = _settings.Cars.First(c => c.Id == carId);
        dtoCar.MinimumSoC = newMinSoc;
    }
}
