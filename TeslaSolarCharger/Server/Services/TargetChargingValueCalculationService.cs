using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class TargetChargingValueCalculationService : ITargetChargingValueCalculationService
{
    private readonly ILogger<TargetChargingValueCalculationService> _logger;
    private readonly ITeslaSolarChargerContext _context;
    private readonly ISettings _settings;

    public TargetChargingValueCalculationService(ILogger<TargetChargingValueCalculationService> logger,
        ITeslaSolarChargerContext context,
        ISettings settings)
    {
        _logger = logger;
        _context = context;
        _settings = settings;
    }

    public async Task SetTargetValues(List<DtoTargetChargingValues> targetChargingValues,
        List<DtoChargingSchedule> activeChargingSchedules, DateTimeOffset currentDate, int powerToControl,
        CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({@targetChargingValues}, {@activeChargingSchedules}, {currentDate})", nameof(SetTargetValues), targetChargingValues, activeChargingSchedules, currentDate);

        foreach (var loadPoint in targetChargingValues
                     .Where(t => activeChargingSchedules.Any(c => c.CarId == t.LoadPoint.CarId && c.OccpChargingConnectorId == t.LoadPoint.ChargingConnectorId)))
        {
            var constraintValues = await GetConstraintValues(loadPoint.LoadPoint.CarId,
                loadPoint.LoadPoint.ChargingConnectorId, loadPoint.LoadPoint.ManageChargingPowerByCar,
                cancellationToken).ConfigureAwait(false);
        }

        var ascending = powerToControl > 0;
        foreach (var loadPoint in (ascending
                     ? targetChargingValues.Where(t => t.Values == default).OrderBy(x => x.LoadPoint.ChargingPriority)
                     : targetChargingValues.Where(t => t.Values == default).OrderByDescending(x => x.LoadPoint.ChargingPriority)))
        {
            var constraintValues = await GetConstraintValues(loadPoint.LoadPoint.CarId,
                loadPoint.LoadPoint.ChargingConnectorId, loadPoint.LoadPoint.ManageChargingPowerByCar,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<ConstraintValues> GetConstraintValues(int? carId, int? connectorId, bool useCarToManageChargingSpeed, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({carId}, {connectorId})", nameof(GetConstraintValues), carId, connectorId);
        var constraintValues = new ConstraintValues();
        if (carId != default)
        {
            var carConfigValues = await _context.Cars
                .Where(c => c.Id == carId.Value)
                .Select(c => new
                {
                    c.MinimumAmpere,
                    c.MaximumAmpere,
                    c.ChargeMode,
                    c.MaximumSoc,
                })
                .FirstAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            constraintValues.MinCurrent = carConfigValues.MinimumAmpere;
            constraintValues.MaxCurrent = carConfigValues.MaximumAmpere;
            constraintValues.ChargeMode = carConfigValues.ChargeMode;
            var car = _settings.Cars.First(c => c.Id == carId);
            constraintValues.MinPhases = car.ActualPhases;
            constraintValues.MaxPhases = car.ActualPhases;
            constraintValues.MaxSoc = carConfigValues.MaximumSoc > car.SocLimit ? car.SocLimit.Value : carConfigValues.MaximumSoc;
        }

        // ReSharper disable once InvertIf
        if (connectorId != default)
        {
            var chargingConnectorConfigValues = await _context.OcppChargingStationConnectors
                .Where(c => c.Id == connectorId.Value)
                .Select(c => new
                {
                    c.MinCurrent,
                    c.MaxCurrent,
                    c.ConnectedPhasesCount,
                    c.AutoSwitchBetween1And3PhasesEnabled,
                    c.ChargeMode,
                })
                .FirstAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (constraintValues.MinCurrent == default)
            {
                constraintValues.MinCurrent = chargingConnectorConfigValues.MinCurrent;
            }
            else if ((!useCarToManageChargingSpeed) && (chargingConnectorConfigValues.MinCurrent > constraintValues.MinCurrent))
            {
                constraintValues.MinCurrent = chargingConnectorConfigValues.MinCurrent;
            }

            if (constraintValues.MaxCurrent == default || chargingConnectorConfigValues.MaxCurrent < constraintValues.MaxCurrent)
            {
                constraintValues.MaxCurrent = chargingConnectorConfigValues.MaxCurrent;
            }

            // ReSharper disable once ConvertIfStatementToNullCoalescingAssignment
            if (constraintValues.MinPhases == default)
            {
                constraintValues.MinPhases = chargingConnectorConfigValues.AutoSwitchBetween1And3PhasesEnabled
                    ? 1
                    : chargingConnectorConfigValues.ConnectedPhasesCount;
            }

            // ReSharper disable once ConvertIfStatementToNullCoalescingAssignment
            if (constraintValues.MaxPhases == default)
            {
                constraintValues.MaxPhases = chargingConnectorConfigValues.ConnectedPhasesCount;
            }
            constraintValues.CanChangePhases = chargingConnectorConfigValues.AutoSwitchBetween1And3PhasesEnabled;
        }

        return constraintValues;
    }

    private class ConstraintValues
    {
        public int? MinCurrent { get; set; }
        public int? MaxCurrent { get; set; }
        public int? MinPhases { get; set; }
        public int? MaxPhases { get; set; }
        public bool? CanChangePhases { get; set; }
        public ChargeModeV2? ChargeMode { get; set; }
        public int? MaxSoc { get; set; }
    }
}
