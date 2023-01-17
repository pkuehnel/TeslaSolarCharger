using Microsoft.EntityFrameworkCore;
using System.Text;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.CarValues;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources;

namespace TeslaSolarCharger.Server.Services.ApiServices;

public class IndexService : IIndexService
{
    private readonly ILogger<IndexService> _logger;
    private readonly ISettings _settings;
    private readonly ITeslamateContext _teslamateContext;
    private readonly IChargingCostService _chargingCostService;
    private readonly ToolTipTextKeys _toolTipTextKeys;
    private readonly IChargeTimePlanningService _chargeTimePlanningService;

    public IndexService(ILogger<IndexService> logger, ISettings settings, ITeslamateContext teslamateContext,
        IChargingCostService chargingCostService, ToolTipTextKeys toolTipTextKeys, IChargeTimePlanningService chargeTimePlanningService)
    {
        _logger = logger;
        _settings = settings;
        _teslamateContext = teslamateContext;
        _chargingCostService = chargingCostService;
        _toolTipTextKeys = toolTipTextKeys;
        _chargeTimePlanningService = chargeTimePlanningService;
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
                PluggedIn = enabledCar.CarState.PluggedIn == true,
                IsHome = enabledCar.CarState.IsHomeGeofence == true,
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

    public Dictionary<string, string> GetToolTipTexts()
    {
        return new Dictionary<string, string>()
        {
            { _toolTipTextKeys.InverterPower, "Power your inverter currently delivers." },
            { _toolTipTextKeys.GridPower, "Power at your grid point. Green: Power feeding into grid; Red: Power consuming from grid" },
            { _toolTipTextKeys.HomeBatterySoC, "State of charge of your home battery." },
            { _toolTipTextKeys.HomeBatteryPower, "Power of your home battery. Green: Battery is charging; Red: Battery is discharging" },
            { _toolTipTextKeys.CombinedChargingPower, "Power sum of all cars charging at home." },
            { _toolTipTextKeys.CarName, "Name configured in your car (or VIN if no name defined)." },
            { _toolTipTextKeys.CarSoc, "State of charge" },
            { _toolTipTextKeys.CarSocLimit, "SoC Limit (configured in the car or in the Tesla App)" },
            { _toolTipTextKeys.CarChargingPowerHome, "Power your car is currently charging at home" },
            { _toolTipTextKeys.CarChargedSolarEnergy, "Total charged solar energy" },
            { _toolTipTextKeys.CarChargedGridEnergy, "Total charged grid energy" },
            { _toolTipTextKeys.CarChargeCost, "Total Charge cost. Note: The charge costs are also autoupdated in the charges you find in TeslaMate. This update can take up to 10 minutes after a charge is completed." },
            { _toolTipTextKeys.CarAtHome, "Your car is in your defined GeoFence" },
            { _toolTipTextKeys.CarNotHealthy, "Your car has no optimal internet connection or there is an issue with the Tesla API." },
            { _toolTipTextKeys.CarPluggedIn, "Your car is plugged in" },
            { _toolTipTextKeys.CarChargeMode, "ChargeMode of your car\r\n" +
                                              $"{ChargeMode.MaxPower.ToFriendlyString()}: Your car will charge with the maximum available power.\r\n" +
                                              $"{ChargeMode.PvOnly.ToFriendlyString()}: Your car will charge with solar power only despite you configured a min SoC in combination with a date when this soc should be reached.\r\n" +
                                              $"{ChargeMode.PvAndMinSoc.ToFriendlyString()}: Your car will charge to the configured Min SoC with maximum available power, then it will continue to charge based on available solar power."},
            { _toolTipTextKeys.ServerTime, "This is needed to properly start charging sessions. If this time does not match your current time, check your server time." },
            { _toolTipTextKeys.ServerTimeZone, "This is needed to properly start charging sessions. If this time does not match your timezone, check the set timezone in your docker-compose.yml" },
        };
    }

    public List<DtoCarTopicValue> GetCarDetails(int carId)
    {
        var values = new List<DtoCarTopicValue>();
        var carState = _settings.Cars.First(c => c.Id == carId).CarState;
        var propertiesToExclude = new List<string>()
        {
            nameof(Car.CarState.PlannedChargingSlots),
        };
        foreach (var property in carState.GetType().GetProperties())
        {
            if (propertiesToExclude.Any(p => property.Name.Equals(p)))
            {
                continue;
            }
            values.Add(new DtoCarTopicValue()
            {
                Topic = AddSpacesBeforeCapitalLetters(property.Name),
                Value = property.GetValue(carState, null)?.ToString(),
            });
        }
        return values;
    }

    public List<DtoChargingSlot> GetChargingSlots(int carId)
    {
        _logger.LogTrace("{method}({carId})", nameof(GetChargingSlots), carId);
        _chargeTimePlanningService.PlanChargeTimesForAllCars();
        return _settings.Cars.First(c => c.Id == carId).CarState.PlannedChargingSlots;
    }

    string AddSpacesBeforeCapitalLetters(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        var newText = new StringBuilder(text.Length * 2);
        newText.Append(text[0]);
        for (var i = 1; i < text.Length; i++)
        {
            if (char.IsUpper(text[i]) && text[i - 1] != ' ')
                newText.Append(' ');
            newText.Append(text[i]);
        }
        return newText.ToString();
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
