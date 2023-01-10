using Microsoft.EntityFrameworkCore;
using System.Configuration;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.CarValues;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services.ApiServices;

public class IndexService : IIndexService
{
    private readonly ILogger<IndexService> _logger;
    private readonly ISettings _settings;
    private readonly ITeslamateContext _teslamateContext;
    private readonly IChargingCostService _chargingCostService;

    public IndexService(ILogger<IndexService> logger, ISettings settings, ITeslamateContext teslamateContext,
        IChargingCostService chargingCostService)
    {
        _logger = logger;
        _settings = settings;
        _teslamateContext = teslamateContext;
        _chargingCostService = chargingCostService;
    }

    public DtoPvValues GetPvValues()
    {
        _logger.LogTrace("{method}()", nameof(GetPvValues));
        return new DtoPvValues()
        {
            GridPower = _settings.Overage,
            InverterPower = _settings.InverterPower,
            HomeBatteryPower = _settings.HomeBatteryPower,
            HomeBatterySoc = _settings.HomeBatterySoc,
            CarCombinedChargingPowerAtHome = _settings.Cars.Select(c => c.CarState.ChargingPowerAtHome).Sum(),
        };
    }

    public async Task<List<DtoCarBaseStates>> GetCarBaseStatesOfEnabledCars()
    {
        _logger.LogTrace("{method}()", nameof(GetCarBaseStatesOfEnabledCars));
        var enabledCars = GetEnabledCars();
        var carBaseValues = new List<DtoCarBaseStates>();
        foreach (var enabledCar in enabledCars)
        {
            var dtoCarBaseValues = new DtoCarBaseStates()
            {
                CarId = enabledCar.Id,
                NameOrVin = enabledCar.CarState.Name,
                StateOfCharge = enabledCar.CarState.SoC,
                StateOfChargeLimit = enabledCar.CarState.SocLimit,
                HomeChargePower = enabledCar.CarState.ChargingPowerAtHome,
                PluggedInAtHome = enabledCar.CarState is { IsHomeGeofence: true, PluggedIn: true },
                IsHome = enabledCar.CarState.PluggedIn == true,
                IsAutoFullSpeedCharging = enabledCar.CarState.AutoFullSpeedCharge,
            };
            if (string.IsNullOrEmpty(dtoCarBaseValues.NameOrVin))
            {
                dtoCarBaseValues.NameOrVin = await GetVinByCarId(enabledCar.Id).ConfigureAwait(false);
            }
            dtoCarBaseValues.DtoChargeSummary = await _chargingCostService.GetChargeSummary(enabledCar.Id).ConfigureAwait(false);

            carBaseValues.Add(dtoCarBaseValues);
            
        }
        return carBaseValues;
    }

    public Dictionary<int, DtoCarBaseSettings> GetCarBaseSettingsOfEnabledCars()
    {
        _logger.LogTrace("{method}()", nameof(GetCarBaseSettingsOfEnabledCars));
        var enabledCars = GetEnabledCars();

        return enabledCars.ToDictionary(enabledCar => enabledCar.Id, enabledCar => new DtoCarBaseSettings()
        {
            CarId = enabledCar.Id,
            ChargeMode = enabledCar.CarConfiguration.ChargeMode,
            MinimumStateOfCharge = enabledCar.CarConfiguration.MinimumSoC,
            LatestTimeToReachStateOfCharge = enabledCar.CarConfiguration.LatestTimeToReachSoC,
        });
    }

    public void UpdateCarBaseSettings(DtoCarBaseSettings carBaseSettings)
    {
        var carConfiguration = _settings.Cars
            .Where(c => c.Id == carBaseSettings.CarId)
            .Select(c => c.CarConfiguration).First();
        carConfiguration.ChargeMode = carBaseSettings.ChargeMode;
        carConfiguration.MinimumSoC = carBaseSettings.MinimumStateOfCharge;
        carConfiguration.LatestTimeToReachSoC = carBaseSettings.LatestTimeToReachStateOfCharge;
    }

    private List<Car> GetEnabledCars()
    {
        _logger.LogTrace("{method}()", nameof(GetEnabledCars));
        return _settings.Cars.Where(c => c.CarConfiguration.ShouldBeManaged == true).ToList();
    }

    

    public async Task<string?> GetVinByCarId(int carId)
    {
        _logger.LogTrace("{method}({carId})", nameof(GetVinByCarId), carId);
        return await _teslamateContext.Cars
            .Where(c => c.Id == carId)
            .Select(c => c.Vin).FirstAsync().ConfigureAwait(false);
    }
}
