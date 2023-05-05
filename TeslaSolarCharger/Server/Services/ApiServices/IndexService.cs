using Microsoft.EntityFrameworkCore;
using System.Text;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.CarValues;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources;
using TeslaSolarCharger.SharedBackend.Contracts;

namespace TeslaSolarCharger.Server.Services.ApiServices;

public class IndexService : IIndexService
{
    private readonly ILogger<IndexService> _logger;
    private readonly ISettings _settings;
    private readonly ITeslamateContext _teslamateContext;
    private readonly IChargingCostService _chargingCostService;
    private readonly ToolTipTextKeys _toolTipTextKeys;
    private readonly ILatestTimeToReachSocUpdateService _latestTimeToReachSocUpdateService;
    private readonly IConfigJsonService _configJsonService;
    private readonly IChargeTimeCalculationService _chargeTimeCalculationService;
    private readonly IConstants _constants;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly IDateTimeProvider _dateTimeProvider;

    public IndexService(ILogger<IndexService> logger, ISettings settings, ITeslamateContext teslamateContext,
        IChargingCostService chargingCostService, ToolTipTextKeys toolTipTextKeys,
        ILatestTimeToReachSocUpdateService latestTimeToReachSocUpdateService, IConfigJsonService configJsonService,
        IChargeTimeCalculationService chargeTimeCalculationService,
        IConstants constants, IConfigurationWrapper configurationWrapper, IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _settings = settings;
        _teslamateContext = teslamateContext;
        _chargingCostService = chargingCostService;
        _toolTipTextKeys = toolTipTextKeys;
        _latestTimeToReachSocUpdateService = latestTimeToReachSocUpdateService;
        _configJsonService = configJsonService;
        _chargeTimeCalculationService = chargeTimeCalculationService;
        _constants = constants;
        _configurationWrapper = configurationWrapper;
        _dateTimeProvider = dateTimeProvider;
    }

    public DtoPvValues GetPvValues()
    {
        _logger.LogTrace("{method}()", nameof(GetPvValues));
        int? powerBuffer = _configurationWrapper.PowerBuffer();
        if (_configurationWrapper.FrontendConfiguration()?.InverterValueSource == SolarValueSource.None
            && _configurationWrapper.FrontendConfiguration()?.GridValueSource == SolarValueSource.None)
        {
            powerBuffer = null;
        }

        return new DtoPvValues()
        {
            GridPower = _settings.Overage,
            InverterPower = _settings.InverterPower,
            HomeBatteryPower = _settings.HomeBatteryPower,
            HomeBatterySoc = _settings.HomeBatterySoc,
            PowerBuffer = powerBuffer, 
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
                ChargingSlots = enabledCar.CarState.PlannedChargingSlots,
                State = enabledCar.CarState.State,
            };
            if (string.IsNullOrEmpty(dtoCarBaseValues.NameOrVin))
            {
                dtoCarBaseValues.NameOrVin = await GetVinByCarId(enabledCar.Id).ConfigureAwait(false);
            }
            dtoCarBaseValues.DtoChargeSummary = await _chargingCostService.GetChargeSummary(enabledCar.Id).ConfigureAwait(false);
            if (enabledCar.CarConfiguration.ChargeMode == ChargeMode.SpotPrice)
            {
                dtoCarBaseValues.ChargingNotPlannedDueToNoSpotPricesAvailable =
                    await _chargeTimeCalculationService.IsLatestTimeToReachSocAfterLatestKnownChargePrice(enabledCar.Id).ConfigureAwait(false);
            }

            dtoCarBaseValues.ChargeInformation = GenerateChargeInformation(enabledCar);

            carBaseValues.Add(dtoCarBaseValues);
            
        }
        return carBaseValues;
    }

    private List<DtoChargeInformation> GenerateChargeInformation(Car enabledCar)
    {
        _logger.LogTrace("{method}({carId})", nameof(GenerateChargeInformation), enabledCar.Id);
        if (_settings.Overage == _constants.DefaultOverage || enabledCar.CarState.PlannedChargingSlots.Any(c => c.IsActive))
        {
            return new List<DtoChargeInformation>();
        }

        var result = new List<DtoChargeInformation>();

        if (enabledCar.CarState.IsHomeGeofence != true)
        {
            result.Add(new DtoChargeInformation()
            {
                InfoText = "Car is at home.",
                TimeToDisplay = default,
            });
        }

        if (enabledCar.CarState.PluggedIn != true)
        {
            result.Add(new DtoChargeInformation()
            {
                InfoText = "Car is plugged in.",
                TimeToDisplay = default,
            });
        }

        if (enabledCar.CarState.State != CarStateEnum.Charging
            && enabledCar.CarState.EarliestSwitchOn != null
            && enabledCar.CarState.EarliestSwitchOn > _dateTimeProvider.Now())
        {
            result.Add(new DtoChargeInformation()
            {
                InfoText = "Enough solar power until {0}.",
                TimeToDisplay = enabledCar.CarState.EarliestSwitchOn ?? default,
            });
        }

        if (enabledCar.CarState.State == CarStateEnum.Charging
            && enabledCar.CarState.EarliestSwitchOff != null
            && enabledCar.CarState.EarliestSwitchOff > _dateTimeProvider.Now())
        {
            result.Add(new DtoChargeInformation()
            {
                InfoText = "Not Enough solar power until {0}",
                TimeToDisplay = enabledCar.CarState.EarliestSwitchOff ?? default,
            });
        }

        if (enabledCar.CarState.State != CarStateEnum.Charging
            && (enabledCar.CarState.SocLimit - enabledCar.CarState.SoC) < _constants.MinimumSocDifference)
        {
            result.Add(new DtoChargeInformation()
            {
                InfoText = $"SoC Limit is at least {_constants.MinimumSocDifference}% higher than acutal SoC",
                TimeToDisplay = default,
            });
        }

        return result;
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
            IgnoreLatestTimeToReachSocDate = enabledCar.CarConfiguration.IgnoreLatestTimeToReachSocDate,
        });
    }

    public async Task UpdateCarBaseSettings(DtoCarBaseSettings carBaseSettings)
    {
        var car = _settings.Cars.First(c => c.Id == carBaseSettings.CarId);
        var carConfiguration = car.CarConfiguration;
        carConfiguration.ChargeMode = carBaseSettings.ChargeMode;
        carConfiguration.MinimumSoC = carBaseSettings.MinimumStateOfCharge;
        carConfiguration.IgnoreLatestTimeToReachSocDate = carBaseSettings.IgnoreLatestTimeToReachSocDate;
        carConfiguration.LatestTimeToReachSoC = carBaseSettings.LatestTimeToReachStateOfCharge;
        await _latestTimeToReachSocUpdateService.UpdateAllCars().ConfigureAwait(false);
        await _chargeTimeCalculationService.PlanChargeTimesForAllCars().ConfigureAwait(false);
        await _configJsonService.UpdateCarConfiguration().ConfigureAwait(false);
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
            { _toolTipTextKeys.CarChargeMode, "ChargeMode of your car. Click <a href=\"https://github.com/pkuehnel/TeslaSolarCharger#charge-modes\"  target=\"_blank\">here</a> for details."},
            { _toolTipTextKeys.ServerTime, "This is needed to properly start charging sessions. If this time does not match your current time, check your server time." },
            { _toolTipTextKeys.ServerTimeZone, "This is needed to properly start charging sessions. If this time does not match your timezone, check the set timezone in your docker-compose.yml" },
            { _toolTipTextKeys.PowerBuffer, "Configured Power Buffer" },
        };
    }

    public DtoCarTopicValues GetCarDetails(int carId)
    {
        var nonDateValues = new List<DtoCarTopicValue>();
        var dateValues = new List<DtoCarDateTopics>();
        var dtoCarTopicValues = new DtoCarTopicValues()
        {
            NonDateValues = nonDateValues,
            DateValues = dateValues,
        };
        var carState = _settings.Cars.First(c => c.Id == carId).CarState;
        var propertiesToExclude = new List<string>()
        {
            nameof(Car.CarState.PlannedChargingSlots),
            nameof(Car.CarState.Name),
            nameof(Car.CarState.SocLimit),
            nameof(Car.CarState.SoC),
        };
        foreach (var property in carState.GetType().GetProperties())
        {
            if (propertiesToExclude.Any(p => property.Name.Equals(p)))
            {
                continue;
            }
            if (property.PropertyType == typeof(DateTimeOffset?)
                || property.PropertyType == typeof(DateTimeOffset))
            {
                dtoCarTopicValues.DateValues.Add(new DtoCarDateTopics()
                {
                    Topic = AddSpacesBeforeCapitalLetters(property.Name),
                    DateTime = ((DateTimeOffset?) property.GetValue(carState, null))?.LocalDateTime,
                });
            }
            else if (property.PropertyType == typeof(DateTime?)
                     || property.PropertyType == typeof(DateTime))
            {
                dtoCarTopicValues.DateValues.Add(new DtoCarDateTopics()
                {
                    Topic = AddSpacesBeforeCapitalLetters(property.Name),
                    DateTime = (DateTime?) property.GetValue(carState, null),
                });
            }
            else
            {
                nonDateValues.Add(new DtoCarTopicValue()
                {
                    Topic = AddSpacesBeforeCapitalLetters(property.Name),
                    Value = property.GetValue(carState, null)?.ToString(),
                });
            }
        }
        return dtoCarTopicValues;
    }

    public List<DtoChargingSlot> RecalculateAndGetChargingSlots(int carId)
    {
        _logger.LogTrace("{method}({carId})", nameof(RecalculateAndGetChargingSlots), carId);
        var car = _settings.Cars.First(c => c.Id == carId);
        _chargeTimeCalculationService.UpdatePlannedChargingSlots(car);
        return car.CarState.PlannedChargingSlots;
    }

    public List<DtoChargingSlot> GetChargingSlots(int carId)
    {
        _logger.LogTrace("{method}({carId})", nameof(GetChargingSlots), carId);
        var car = _settings.Cars.First(c => c.Id == carId);
        return car.CarState.PlannedChargingSlots;
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
