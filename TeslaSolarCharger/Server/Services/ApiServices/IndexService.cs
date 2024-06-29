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
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedBackend.Contracts;

namespace TeslaSolarCharger.Server.Services.ApiServices;

public class IndexService : IIndexService
{
    private readonly ILogger<IndexService> _logger;
    private readonly ISettings _settings;
    private readonly ITeslamateContext _teslamateContext;
    private readonly ToolTipTextKeys _toolTipTextKeys;
    private readonly ILatestTimeToReachSocUpdateService _latestTimeToReachSocUpdateService;
    private readonly IConfigJsonService _configJsonService;
    private readonly IChargeTimeCalculationService _chargeTimeCalculationService;
    private readonly IConstants _constants;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ITeslaSolarChargerContext _teslaSolarChargerContext;
    private readonly ITscOnlyChargingCostService _tscOnlyChargingCostService;

    public IndexService(ILogger<IndexService> logger, ISettings settings, ITeslamateContext teslamateContext, ToolTipTextKeys toolTipTextKeys,
        ILatestTimeToReachSocUpdateService latestTimeToReachSocUpdateService, IConfigJsonService configJsonService,
        IChargeTimeCalculationService chargeTimeCalculationService,
        IConstants constants, IConfigurationWrapper configurationWrapper, IDateTimeProvider dateTimeProvider,
        ITeslaSolarChargerContext teslaSolarChargerContext, ITscOnlyChargingCostService tscOnlyChargingCostService)
    {
        _logger = logger;
        _settings = settings;
        _teslamateContext = teslamateContext;
        _toolTipTextKeys = toolTipTextKeys;
        _latestTimeToReachSocUpdateService = latestTimeToReachSocUpdateService;
        _configJsonService = configJsonService;
        _chargeTimeCalculationService = chargeTimeCalculationService;
        _constants = constants;
        _configurationWrapper = configurationWrapper;
        _dateTimeProvider = dateTimeProvider;
        _teslaSolarChargerContext = teslaSolarChargerContext;
        _tscOnlyChargingCostService = tscOnlyChargingCostService;
    }

    public DtoPvValues GetPvValues()
    {
        _logger.LogTrace("{method}()", nameof(GetPvValues));
        int? powerBuffer = _configurationWrapper.PowerBuffer(true);
        if (_settings.InverterPower == null && _settings.Overage == null)
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
            CarCombinedChargingPowerAtHome = _settings.CarsToManage.Select(c => c.ChargingPowerAtHome).Sum(),
            LastUpdated = _settings.LastPvValueUpdate,
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
                Name = enabledCar.Name,
                Vin = enabledCar.Vin,
                StateOfCharge = enabledCar.SoC,
                StateOfChargeLimit = enabledCar.SocLimit,
                HomeChargePower = enabledCar.ChargingPowerAtHome,
                PluggedIn = enabledCar.PluggedIn == true,
                IsHome = enabledCar.IsHomeGeofence == true,
                IsAutoFullSpeedCharging = enabledCar.AutoFullSpeedCharge,
                ChargingSlots = enabledCar.PlannedChargingSlots,
                State = enabledCar.State,
            };
            dtoCarBaseValues.DtoChargeSummary = await _tscOnlyChargingCostService.GetChargeSummary(enabledCar.Id).ConfigureAwait(false);
            if (enabledCar.ChargeMode == ChargeMode.SpotPrice)
            {
                dtoCarBaseValues.ChargingNotPlannedDueToNoSpotPricesAvailable =
                    await _chargeTimeCalculationService.IsLatestTimeToReachSocAfterLatestKnownChargePrice(enabledCar.Id).ConfigureAwait(false);
            }

            var dbCar = await _teslaSolarChargerContext.Cars.Where(c => c.Id == enabledCar.Id).SingleAsync();
            dtoCarBaseValues.FleetApiState = dbCar.TeslaFleetApiState;
            dtoCarBaseValues.VehicleCommandProtocolRequired = dbCar.VehicleCommandProtocolRequired;
            dtoCarBaseValues.RateLimitedUntil = dbCar.RateLimitedUntil;

            dtoCarBaseValues.ChargeInformation = GenerateChargeInformation(enabledCar);

            carBaseValues.Add(dtoCarBaseValues);
            
        }
        return carBaseValues;
    }

    private List<DtoChargeInformation> GenerateChargeInformation(DtoCar enabledDtoCar)
    {
        _logger.LogTrace("{method}({carId})", nameof(GenerateChargeInformation), enabledDtoCar.Id);
        if (_settings.Overage == _constants.DefaultOverage || enabledDtoCar.PlannedChargingSlots.Any(c => c.IsActive))
        {
            return new List<DtoChargeInformation>();
        }

        var result = new List<DtoChargeInformation>();

        if (enabledDtoCar.IsHomeGeofence != true)
        {
            result.Add(new DtoChargeInformation()
            {
                InfoText = "Car is at home.",
                TimeToDisplay = default,
            });
        }

        if (enabledDtoCar.PluggedIn != true)
        {
            result.Add(new DtoChargeInformation()
            {
                InfoText = "Car is plugged in.",
                TimeToDisplay = default,
            });
        }

        if ((!(enabledDtoCar.State == CarStateEnum.Charging && enabledDtoCar.IsHomeGeofence == true))
            && enabledDtoCar.EarliestSwitchOn != null
            && enabledDtoCar.EarliestSwitchOn > _dateTimeProvider.Now())
        {
            result.Add(new DtoChargeInformation()
            {
                InfoText = "Enough solar power until {0}.",
                TimeToDisplay = enabledDtoCar.EarliestSwitchOn ?? default,
            });
        }

        if ((!(enabledDtoCar.State == CarStateEnum.Charging && enabledDtoCar.IsHomeGeofence == true))
            && enabledDtoCar.EarliestSwitchOn == null)
        {
            result.Add(new DtoChargeInformation()
            {
                InfoText = $"Enough solar power for at least {_configurationWrapper.TimespanUntilSwitchOn().TotalMinutes} minutes.",
                TimeToDisplay = default,
            });
        }

        if ((enabledDtoCar.State == CarStateEnum.Charging && enabledDtoCar.IsHomeGeofence == true)
            && enabledDtoCar.EarliestSwitchOff != null
            && enabledDtoCar.EarliestSwitchOff > _dateTimeProvider.Now())
        {
            result.Add(new DtoChargeInformation()
            {
                InfoText = "Not Enough solar power until {0}",
                TimeToDisplay = enabledDtoCar.EarliestSwitchOff ?? default,
            });
        }

        if ((!(enabledDtoCar.State == CarStateEnum.Charging && enabledDtoCar.IsHomeGeofence == true))
            && (enabledDtoCar.SocLimit - enabledDtoCar.SoC) < (_constants.MinimumSocDifference + 1))
        {
            result.Add(new DtoChargeInformation()
            {
                InfoText = $"SoC Limit is at least {_constants.MinimumSocDifference + 1}% higher than actual SoC",
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
            ChargeMode = enabledCar.ChargeMode,
            MinimumStateOfCharge = enabledCar.MinimumSoC,
            LatestTimeToReachStateOfCharge = enabledCar.LatestTimeToReachSoC,
            IgnoreLatestTimeToReachSocDate = enabledCar.IgnoreLatestTimeToReachSocDate,
        });
    }

    public async Task UpdateCarBaseSettings(DtoCarBaseSettings carBaseSettings)
    {
        await _latestTimeToReachSocUpdateService.UpdateAllCars().ConfigureAwait(false);
        await _chargeTimeCalculationService.PlanChargeTimesForAllCars().ConfigureAwait(false);
        await _configJsonService.UpdateCarBaseSettings(carBaseSettings).ConfigureAwait(false);
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
            { _toolTipTextKeys.CarChargedHomeBatteryEnergy, "Total charged home battery energy" },
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
        var carState = _settings.Cars.First(c => c.Id == carId);
        var propertiesToExclude = new List<string>()
        {
            nameof(DtoCar.PlannedChargingSlots),
            nameof(DtoCar.Name),
            nameof(DtoCar.SocLimit),
            nameof(DtoCar.SoC),
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
        return car.PlannedChargingSlots;
    }

    public List<DtoChargingSlot> GetChargingSlots(int carId)
    {
        _logger.LogTrace("{method}({carId})", nameof(GetChargingSlots), carId);
        var car = _settings.Cars.First(c => c.Id == carId);
        return car.PlannedChargingSlots;
    }

    public async Task UpdateCarFleetApiState(int carId, TeslaCarFleetApiState fleetApiState)
    {
        _logger.LogTrace("{method}({carId}, {fleetApiState})", nameof(UpdateCarFleetApiState), carId, fleetApiState);
        var car = _teslaSolarChargerContext.Cars.First(c => c.Id == carId);
        car.TeslaFleetApiState = fleetApiState;
        await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
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

    private List<DtoCar> GetEnabledCars()
    {
        _logger.LogTrace("{method}()", nameof(GetEnabledCars));
        return _settings.CarsToManage;
    }

    

    public async Task<string?> GetVinByCarId(int carId)
    {
        _logger.LogTrace("{method}({carId})", nameof(GetVinByCarId), carId);
        return await _teslaSolarChargerContext.Cars
            .Where(c => c.Id == carId)
            .Select(c => c.Vin).FirstAsync().ConfigureAwait(false);
    }
}
