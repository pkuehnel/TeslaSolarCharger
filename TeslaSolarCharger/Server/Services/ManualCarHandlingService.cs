using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class ManualCarHandlingService(
    ILogger<ManualCarHandlingService> logger,
    ITeslaSolarChargerContext context,
    ISettings settings,
    IDateTimeProvider dateTimeProvider) : IManualCarHandlingService
{
    public async Task UpdateStateOfChargeAsync(int carId, int newStateOfCharge)
    {
        logger.LogTrace("{method}({carId}, {stateOfCharge})", nameof(UpdateStateOfChargeAsync), carId, newStateOfCharge);
        if (newStateOfCharge < 0 || newStateOfCharge > 100)
        {
            throw new InvalidOperationException("State of charge must be between 0 and 100%.");
        }

        var car = await context.Cars.FirstOrDefaultAsync(c => c.Id == carId).ConfigureAwait(false);
        if (car == null)
        {
            throw new InvalidOperationException($"Car with id {carId} not found.");
        }

        if (car.CarType != CarType.Manual)
        {
            throw new InvalidOperationException("State of charge can only be set manually for manual cars.");
        }

        var timestamp = dateTimeProvider.DateTimeOffSetUtcNow();
        var carValueLog = new CarValueLog
        {
            CarId = carId,
            Type = CarValueType.StateOfCharge,
            IntValue = newStateOfCharge,
            Timestamp = timestamp.UtcDateTime,
            Source = CarValueSource.Manual,
        };

        context.CarValueLogs.Add(carValueLog);
        await context.SaveChangesAsync().ConfigureAwait(false);

        var cachedCar = settings.Cars.FirstOrDefault(c => c.Id == carId);
        if (cachedCar == default)
        {
            logger.LogWarning("Settings entry for car {carId} was not found while updating manual SoC.", carId);
            return;
        }

        cachedCar.SoC.Update(timestamp, newStateOfCharge);
    }

    public async Task<ManualCarOperationResult> UpdateStateFromConnectorAsync(int carId, DtoOcppConnectorState connectorState)
    {
        logger.LogTrace("{method}({carId})", nameof(UpdateStateFromConnectorAsync), carId);
        if (!await IsManualCarAsync(carId).ConfigureAwait(false))
        {
            return ManualCarOperationResult.NotManual;
        }

        var cachedCar = TryGetCachedCar(carId, throwIfMissing: false);
        if (cachedCar == default)
        {
            return new(true, false);
        }

        var valueLogs = new List<CarValueLog>();

        var pluggedTimestamp = connectorState.IsPluggedIn.Timestamp;
        var pluggedInChanged = cachedCar.PluggedIn.Update(connectorState.IsPluggedIn.Timestamp, connectorState.IsPluggedIn.Value, true);
        var stateChanged = pluggedInChanged;
        if (pluggedInChanged)
        {
            var newValue = connectorState.IsPluggedIn.Value;
            valueLogs.Add(CreateBooleanLog(carId, CarValueType.IsPluggedIn, pluggedTimestamp, newValue, CarValueSource.LinkedCharger));
            if (pluggedInChanged)
            {
                cachedCar.SoC.Update(pluggedTimestamp, null);
            }
        }

        var chargingTimestamp = connectorState.IsCharging.Timestamp;
        var isChargingChanged = cachedCar.IsCharging.Update(connectorState.IsCharging.Timestamp, connectorState.IsCharging.Value, true);
        if (isChargingChanged)
        {
            valueLogs.Add(CreateBooleanLog(carId, CarValueType.IsCharging, chargingTimestamp, connectorState.IsCharging.Value, CarValueSource.LinkedCharger));
            stateChanged = true;
        }

        if (valueLogs.Count > 0)
        {
            context.CarValueLogs.AddRange(valueLogs);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
        return new(true, stateChanged);
    }

    public async Task<ManualCarOperationResult> HandleConnectorAssignmentAsync(int carId, bool? isCharging, DateTimeOffset timestamp)
    {
        logger.LogTrace("{method}({carId})", nameof(HandleConnectorAssignmentAsync), carId);
        if (!await IsManualCarAsync(carId).ConfigureAwait(false))
        {
            return ManualCarOperationResult.NotManual;
        }

        var cachedCar = TryGetCachedCar(carId, throwIfMissing: true)!;

        var logs = new List<CarValueLog>
        {
            CreateBooleanLog(carId, CarValueType.IsPluggedIn, timestamp, true, CarValueSource.LinkedCharger),
            CreateBooleanLog(carId, CarValueType.IsCharging, timestamp, isCharging, CarValueSource.LinkedCharger),
        };

        context.CarValueLogs.AddRange(logs);
        await context.SaveChangesAsync().ConfigureAwait(false);

        var stateChanged = cachedCar.PluggedIn.Update(timestamp, true, true);
        stateChanged = cachedCar.IsCharging.Update(timestamp, isCharging, true) || stateChanged;
        stateChanged = cachedCar.IsHomeGeofence.Update(timestamp, true, true) || stateChanged;
        stateChanged = cachedCar.SoC.Update(timestamp, null) || stateChanged;

        return new(true, stateChanged);
    }

    public async Task<ManualCarOperationResult> HandleConnectorUnassignmentAsync(int carId, DateTimeOffset timestamp)
    {
        logger.LogTrace("{method}({carId})", nameof(HandleConnectorUnassignmentAsync), carId);
        if (!await IsManualCarAsync(carId).ConfigureAwait(false))
        {
            return ManualCarOperationResult.NotManual;
        }

        var cachedCar = TryGetCachedCar(carId, throwIfMissing: true)!;

        var logs = new List<CarValueLog>
        {
            CreateBooleanLog(carId, CarValueType.IsPluggedIn, timestamp, false, CarValueSource.LinkedCharger),
            CreateBooleanLog(carId, CarValueType.IsCharging, timestamp, false, CarValueSource.LinkedCharger),
        };

        context.CarValueLogs.AddRange(logs);
        await context.SaveChangesAsync().ConfigureAwait(false);

       var stateChanged = cachedCar.PluggedIn.Update(timestamp, false);
       stateChanged = cachedCar.IsCharging.Update(timestamp, false) || stateChanged;

        return new(true, stateChanged);
    }

    private async Task<bool> IsManualCarAsync(int carId)
        => await context.Cars.AnyAsync(c => c.Id == carId && c.CarType == CarType.Manual).ConfigureAwait(false);

    private DtoCar? TryGetCachedCar(int carId, bool throwIfMissing)
    {
        var car = settings.Cars.FirstOrDefault(c => c.Id == carId);
        if (car != default)
        {
            return car;
        }

        if (throwIfMissing)
        {
            throw new InvalidOperationException($"Settings entry for car {carId} was not found.");
        }

        logger.LogWarning("Settings entry for car {carId} was not found while updating manual car state.", carId);
        return null;
    }

    private static CarValueLog CreateBooleanLog(int carId, CarValueType type, DateTimeOffset timestamp, bool? value, CarValueSource source)
        => new()
        {
            CarId = carId,
            Type = type,
            Timestamp = timestamp.UtcDateTime,
            BooleanValue = value,
            Source = source,
        };
}
