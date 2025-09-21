﻿using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.ChargepointAction;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Helper.Contracts;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class HomeService : IHomeService
{
    private readonly ILogger<HomeService> _logger;
    private readonly ITeslaSolarChargerContext _context;
    private readonly ISettings _settings;
    private readonly IOcppChargePointActionService _ocppChargePointActionService;
    private readonly IConstants _constants;
    private readonly ITscOnlyChargingCostService _tscOnlyChargingCostService;
    private readonly IValidFromToHelper _validFromToHelper;
    private readonly ILoadPointManagementService _loadPointManagementService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public HomeService(ILogger<HomeService> logger,
        ITeslaSolarChargerContext context,
        ISettings settings,
        IOcppChargePointActionService ocppChargePointActionService,
        IConstants constants,
        ITscOnlyChargingCostService tscOnlyChargingCostService,
        IValidFromToHelper validFromToHelper,
        ILoadPointManagementService loadPointManagementService,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _context = context;
        _settings = settings;
        _ocppChargePointActionService = ocppChargePointActionService;
        _constants = constants;
        _tscOnlyChargingCostService = tscOnlyChargingCostService;
        _validFromToHelper = validFromToHelper;
        _loadPointManagementService = loadPointManagementService;
        _dateTimeProvider = dateTimeProvider;
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

    public async Task<DtoCarOverviewSettings> GetCarOverview(int carId)
    {
        _logger.LogTrace("{method}({carId})", nameof(GetCarOverview), carId);
        var dbCar = await _context.Cars
            .Where(c => c.Id == carId)
            .Select(c => new
            {
                c.Name,
                c.Vin,
                c.MinimumSoc,
                c.MaximumSoc,
                c.ChargeMode,
                c.CarType,
            })
            .FirstAsync()
            .ConfigureAwait(false);
        var carOverView = new DtoCarOverviewSettings(dbCar.Name ?? dbCar.Vin ?? "Unknown name")
        {
            MinSoc = dbCar.MinimumSoc,
            MaxSoc = dbCar.MaximumSoc,
            ChargeMode = dbCar.ChargeMode,
            CarType = dbCar.CarType,
        };
        return carOverView;
    }

    public async Task<DtoChargingConnectorOverviewSettings> GetChargingConnectorOverview(int chargingConnectorId)
    {
        _logger.LogTrace("{method}({chargingConnectorId})", nameof(GetChargingConnectorOverview), chargingConnectorId);
        var chargingConnectorData = await _context.OcppChargingStationConnectors
            .Where(c => c.Id == chargingConnectorId)
            .Select(c => new
            {
                c.Name,
                c.ChargeMode,
            })
            .FirstAsync();
        var chargingConnector = new DtoChargingConnectorOverviewSettings(chargingConnectorData.Name)
        {
            ChargeMode = chargingConnectorData.ChargeMode,
        };
        return chargingConnector;
    }

    public List<DtoChargingSchedule> GetChargingSchedules(int? carId, int? chargingConnectorId)
    {
        _logger.LogTrace("{method}({carId}, {chargingConnectorId})", nameof(GetChargingSchedules), carId, chargingConnectorId);
        var elements = _settings.ChargingSchedules
            .Where(c => c.CarId == carId && c.OcppChargingConnectorId == chargingConnectorId)
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
        dbValue.LastFulFilled = null;
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

    public async Task UpdateCarMaxSoc(int carId, int newSoc)
    {
        _logger.LogTrace("{method}({carId}, {newSoc})", nameof(UpdateCarMaxSoc), carId, newSoc);
        var dbCar = await _context.Cars.FirstAsync(c => c.Id == carId).ConfigureAwait(false);
        dbCar.MaximumSoc = newSoc;
        await _context.SaveChangesAsync();
    }

    public async Task UpdateManualCarSoc(int carId, int newSoc)
    {
        _logger.LogTrace("{method}({carId}, {newSoc})", nameof(UpdateManualCarSoc), carId, newSoc);
        if (newSoc < 0 || newSoc > 100)
        {
            throw new InvalidOperationException("State of charge must be between 0 and 100%.");
        }

        var dbCar = await _context.Cars.FirstOrDefaultAsync(c => c.Id == carId).ConfigureAwait(false);
        if (dbCar == null)
        {
            throw new InvalidOperationException($"Car with id {carId} not found.");
        }

        if (dbCar.CarType != CarType.Manual)
        {
            throw new InvalidOperationException("State of charge can only be set manually for manual cars.");
        }

        var timestamp = _dateTimeProvider.DateTimeOffSetUtcNow();

        var carValueLog = new CarValueLog
        {
            CarId = carId,
            Type = CarValueType.StateOfCharge,
            IntValue = newSoc,
            Timestamp = timestamp.UtcDateTime,
            Source = CarValueSource.Manual,
        };
        _context.CarValueLogs.Add(carValueLog);
        await _context.SaveChangesAsync().ConfigureAwait(false);

        var dtoCar = _settings.Cars.FirstOrDefault(c => c.Id == carId);
        if (dtoCar == default)
        {
            _logger.LogWarning("Settings entry for car {carId} was not found while updating manual SoC.", carId);
        }
        else
        {
            dtoCar.SoC.Update(timestamp, newSoc);
        }

        await _loadPointManagementService.CarStateChanged(carId).ConfigureAwait(false);
    }

    public Dictionary<int, string> GetLoadPointCarOptions()
    {
        _logger.LogTrace("{method}()", nameof(GetLoadPointCarOptions));
        var result = new Dictionary<int, string>
        {
            [0] = _constants.UnknownCarName,
        };
        foreach (var managedCar in _settings.CarsToManage.OrderBy(car => car.ChargingPriority))
        {
            result[managedCar.Id] = managedCar.Name ?? managedCar.Vin;
        }
        return result;
    }

    public async Task<Dictionary<DateTimeOffset, decimal>> GetGridPrices(DateTimeOffset from, DateTimeOffset to)
    {
        _logger.LogTrace("{method}({from}, {to})", nameof(GetGridPrices), from, to);
        var startOfFirstHour = new DateTimeOffset(from.Year, from.Month, from.Day, from.Hour, 0, 0, from.Offset);
        var gridPrices = await _tscOnlyChargingCostService.GetPricesInTimeSpan(startOfFirstHour, to).ConfigureAwait(false);
        var hourlyAverageGridPrices = _validFromToHelper.GetHourlyAverages(gridPrices, from, to, price => price.GridPrice, false);
        return hourlyAverageGridPrices;
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
