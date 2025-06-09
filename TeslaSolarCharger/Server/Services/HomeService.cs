using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Server.Services.ChargepointAction;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class HomeService : IHomeService
{
    private readonly ILogger<HomeService> _logger;
    private readonly ITeslaSolarChargerContext _context;
    private readonly ISettings _settings;
    private readonly IOcppChargePointActionService _ocppChargePointActionService;

    public HomeService(ILogger<HomeService> logger,
        ITeslaSolarChargerContext context,
        ISettings settings,
        IOcppChargePointActionService ocppChargePointActionService)
    {
        _logger = logger;
        _context = context;
        _settings = settings;
        _ocppChargePointActionService = ocppChargePointActionService;
    }

    public async Task<DtoCarChargingTarget> GetChargingTarget(int chargingTargetId)
    {
        _logger.LogTrace("{method}({chargingTargetId})", nameof(GetChargingTarget), chargingTargetId);
        return await _context.CarChargingTargets
            .Where(s => s.Id == chargingTargetId)
            .Select(ToDto)
            .FirstAsync()
            .ConfigureAwait(false);
    }
    
    public async Task<List<DtoCarChargingTarget>> GetCarChargingTargets(int carId)
    {
        _logger.LogTrace("{method}({carId})", nameof(GetCarChargingTargets), carId);
        return await _context.CarChargingTargets
            .Where(s => s.CarId == carId)
            .Select(ToDto)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public DtoCarOverview GetCarOverview(int carId)
    {
        _logger.LogTrace("{method}({carId})", nameof(GetCarOverview), carId);
        var dtoCar = _settings.Cars.First(c => c.Id == carId);
        var carOverView = new DtoCarOverview(dtoCar.Name ?? dtoCar.Vin)
        {
            Soc = dtoCar.SoC,
            CarSideSocLimit = dtoCar.SocLimit,
            MinSoc = dtoCar.MinimumSoC,
            MaxSoc = dtoCar.MaximumSoC.Value,
            ChargeMode = dtoCar.ChargeModeV2,
            IsCharging = dtoCar.State == CarStateEnum.Charging,
            IsHome = dtoCar.IsHomeGeofence == true,
            IsPluggedIn = dtoCar.PluggedIn == true,
        };
        return carOverView;
    }

    public async Task<DtoChargingConnectorOverview> GetChargingConnectorOverview(int chargingConnectorId)
    {
        _logger.LogTrace("{method}({chargingConnectorId})", nameof(GetChargingConnectorOverview), chargingConnectorId);
        var state = _settings.OcppConnectorStates.GetValueOrDefault(chargingConnectorId);
        var chargingConnectorData = await _context.OcppChargingStationConnectors
            .Where(c => c.Id == chargingConnectorId)
            .Select(c => new
            {
                c.Name,
                c.ChargeMode,
            })
            .FirstAsync();
        var chargingConnector = new DtoChargingConnectorOverview(chargingConnectorData.Name)
        {
            IsCharging = state != default && state.IsCharging.Value,
            IsPluggedIn = state != default && state.IsPluggedIn.Value,
            ChargeMode = chargingConnectorData.ChargeMode,
        };
        return chargingConnector;
    }

    public List<DtoChargingSchedule> GetChargingSchedules(int? carId, int? chargingConnectorId)
    {
        _logger.LogTrace("{method}({carId}, {chargingConnectorId})", nameof(GetChargingSchedules), carId, chargingConnectorId);
        var elements = _settings.ChargingSchedules
            .Where(c => c.CarId == carId && c.OccpChargingConnectorId == chargingConnectorId)
            .OrderBy(c => c.ValidFrom)
            .ToList();
        return elements;
    }

    private static readonly Expression<Func<CarChargingTarget, DtoCarChargingTarget>> ToDto =
        s => new DtoCarChargingTarget
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

    public async Task<Result<int>> SaveCarChargingTarget(int carId, DtoCarChargingTarget dto)
    {
        _logger.LogTrace("{method}({carId}, {@dto})", nameof(SaveCarChargingTarget), carId, dto);
        var dbValue = await _context.CarChargingTargets
            .FirstOrDefaultAsync(s => s.Id == dto.Id).ConfigureAwait(false);
        if (dbValue == null)
        {
            dbValue = new();
            _context.CarChargingTargets.Add(dbValue);
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

    public async Task DeleteCarChargingTarget(int chargingTargetId)
    {
        _logger.LogTrace("{method}({chargingTargetId})", nameof(DeleteCarChargingTarget), chargingTargetId);
        _context.CarChargingTargets.Remove(new() { Id = chargingTargetId });
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

    public async Task UpdateCarChargeMode(int carId, ChargeModeV2 chargeMode)
    {
        _logger.LogTrace("{method}({carId}, {minSoc})", nameof(UpdateCarChargeMode), carId, chargeMode);
        var dbCar = await _context.Cars.FirstAsync(c => c.Id == carId).ConfigureAwait(false);
        dbCar.ChargeMode = chargeMode;
        await _context.SaveChangesAsync();
        var dtoCar = _settings.Cars.First(c => c.Id == carId);
        dtoCar.ChargeModeV2 = chargeMode;
    }

    public async Task UpdateChargingConnectorChargeMode(int chargingConnectorId, ChargeModeV2 chargeMode)
    {
        _logger.LogTrace("{method}({chargingConnectorId}, {minSoc})", nameof(UpdateChargingConnectorChargeMode), chargingConnectorId, chargeMode);
        var dbChargingConnector = await _context.OcppChargingStationConnectors
            .FirstAsync(c => c.Id == chargingConnectorId).ConfigureAwait(false);
        dbChargingConnector.ChargeMode = chargeMode;
        await _context.SaveChangesAsync();
    }

    public async Task StartChargingConnectorCharging(int chargingConnectorId, int currentToSet, int? numberOfPhases, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({chargingConnectorId}, {currentToSet}, {numberOfPhases})", nameof(StartChargingConnectorCharging), chargingConnectorId, currentToSet, numberOfPhases);
        var result = await _ocppChargePointActionService.StartCharging(chargingConnectorId, currentToSet, numberOfPhases, cancellationToken);
        if (result.HasError)
        {
            throw new InvalidOperationException(result.ErrorMessage);
        }
    }

    public async Task SetChargingConnectorCurrent(int chargingConnectorId, int currentToSet, int? numberOfPhases, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({chargingConnectorId}, {currentToSet}, {numberOfPhases})", nameof(SetChargingConnectorCurrent), chargingConnectorId, currentToSet, numberOfPhases);
        var result = await _ocppChargePointActionService.SetChargingCurrent(chargingConnectorId, currentToSet, numberOfPhases, cancellationToken);
        if (result.HasError)
        {
            throw new InvalidOperationException(result.ErrorMessage);
        }
    }

    public async Task StopChargingConnectorCharging(int chargingConnectorId, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({chargingConnectorId})", nameof(StopChargingConnectorCharging), chargingConnectorId);
        var result = await _ocppChargePointActionService.StopCharging(chargingConnectorId, cancellationToken);
        if (result.HasError)
        {
            throw new InvalidOperationException(result.ErrorMessage);
        }
    }
}
