using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
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

namespace TeslaSolarCharger.Server.Services.ApiServices;

public class IndexService(
    ILogger<IndexService> logger,
    ISettings settings,
    ToolTipTextKeys toolTipTextKeys,
    ILatestTimeToReachSocUpdateService latestTimeToReachSocUpdateService,
    IConfigJsonService configJsonService,
    IChargeTimeCalculationService chargeTimeCalculationService,
    IConstants constants,
    IConfigurationWrapper configurationWrapper,
    IDateTimeProvider dateTimeProvider,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    ITscOnlyChargingCostService tscOnlyChargingCostService,
    ILoadPointManagementService loadPointManagementService)
    : IIndexService
{
    public DtoPvValues GetPvValues()
    {
        logger.LogTrace("{method}()", nameof(GetPvValues));
        int? powerBuffer = configurationWrapper.PowerBuffer();
        if (settings.InverterPower == null && settings.Overage == null)
        {
            powerBuffer = null;
        }
        var loadPoints = loadPointManagementService.GetLoadPointsWithChargingDetails();
        var pvValues = new DtoPvValues()
        {
            GridPower = settings.Overage,
            InverterPower = settings.InverterPower,
            HomeBatteryPower = settings.HomeBatteryPower,
            HomeBatterySoc = settings.HomeBatterySoc,
            PowerBuffer = powerBuffer,
            CarCombinedChargingPowerAtHome = loadPoints.Select(l => l.ChargingPower).Sum(),
            LastUpdated = settings.LastPvValueUpdate,
        };
        return pvValues;
    }

    public async Task<List<DtoCarBaseStates>> GetCarBaseStatesOfEnabledCars()
    {
        logger.LogTrace("{method}()", nameof(GetCarBaseStatesOfEnabledCars));
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
                ModuleTemperatureMin = enabledCar.MinBatteryTemperature.Value,
                ModuleTemperatureMax = enabledCar.MaxBatteryTemperature.Value,
            };
            dtoCarBaseValues.DtoChargeSummary = await tscOnlyChargingCostService.GetChargeSummary(enabledCar.Id, null).ConfigureAwait(false);
            if (enabledCar.ChargeMode == ChargeMode.SpotPrice)
            {
                dtoCarBaseValues.ChargingNotPlannedDueToNoSpotPricesAvailable =
                    await chargeTimeCalculationService.IsLatestTimeToReachSocAfterLatestKnownChargePrice(enabledCar.Id).ConfigureAwait(false);
            }

            var dbCar = await teslaSolarChargerContext.Cars.Where(c => c.Id == enabledCar.Id).SingleAsync();
            dtoCarBaseValues.FleetApiState = dbCar.TeslaFleetApiState;
            dtoCarBaseValues.ChargeInformation = GenerateChargeInformation(enabledCar);

            carBaseValues.Add(dtoCarBaseValues);
            
        }
        return carBaseValues;
    }

    private List<DtoChargeInformation> GenerateChargeInformation(DtoCar enabledDtoCar)
    {
        logger.LogTrace("{method}({carId})", nameof(GenerateChargeInformation), enabledDtoCar.Id);
        if (settings.Overage == constants.DefaultOverage || enabledDtoCar.PlannedChargingSlots.Any(c => c.IsActive))
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
            && enabledDtoCar.EarliestSwitchOn > dateTimeProvider.Now())
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
                InfoText = $"Enough solar power for at least {configurationWrapper.TimespanUntilSwitchOn().TotalMinutes} minutes.",
                TimeToDisplay = default,
            });
        }

        if ((enabledDtoCar.State == CarStateEnum.Charging && enabledDtoCar.IsHomeGeofence == true)
            && enabledDtoCar.EarliestSwitchOff != null
            && enabledDtoCar.EarliestSwitchOff > dateTimeProvider.Now())
        {
            result.Add(new DtoChargeInformation()
            {
                InfoText = "Not Enough solar power until {0}",
                TimeToDisplay = enabledDtoCar.EarliestSwitchOff ?? default,
            });
        }

        if ((!(enabledDtoCar.State == CarStateEnum.Charging && enabledDtoCar.IsHomeGeofence == true))
            && (enabledDtoCar.SocLimit - enabledDtoCar.SoC) < (constants.MinimumSocDifference + 1))
        {
            result.Add(new DtoChargeInformation()
            {
                InfoText = $"SoC Limit is at least {constants.MinimumSocDifference + 1}% higher than actual SoC",
                TimeToDisplay = default,
            });
        }

        return result;
    }

    public Dictionary<int, DtoCarBaseSettings> GetCarBaseSettingsOfEnabledCars()
    {
        logger.LogTrace("{method}()", nameof(GetCarBaseSettingsOfEnabledCars));
        var enabledCars = GetEnabledCars();

        return enabledCars.ToDictionary(enabledCar => enabledCar.Id, enabledCar => new DtoCarBaseSettings()
        {
            CarId = enabledCar.Id,
            ChargeMode = enabledCar.ChargeMode,
            MinimumStateOfCharge = enabledCar.MinimumSoC,
            LatestTimeToReachStateOfCharge = enabledCar.LatestTimeToReachSoC,
            IgnoreLatestTimeToReachSocDate = enabledCar.IgnoreLatestTimeToReachSocDate,
            IgnoreLatestTimeToReachSocDateOnWeekend = enabledCar.IgnoreLatestTimeToReachSocDateOnWeekend,
        });
    }

    public async Task UpdateCarBaseSettings(DtoCarBaseSettings carBaseSettings)
    {
        await latestTimeToReachSocUpdateService.UpdateAllCars().ConfigureAwait(false);
        await chargeTimeCalculationService.PlanChargeTimesForAllCars().ConfigureAwait(false);
        await configJsonService.UpdateCarBaseSettings(carBaseSettings).ConfigureAwait(false);
    }

    public Dictionary<string, string> GetToolTipTexts()
    {
        return new Dictionary<string, string>()
        {
            { toolTipTextKeys.CarName, "Name configured in your car (or VIN if no name defined)." },
            { toolTipTextKeys.CarSoc, "State of charge" },
            { toolTipTextKeys.CarSocLimit, "SoC Limit (configured in the car or in the Tesla App)" },
            { toolTipTextKeys.CarChargingPowerHome, "Power your car is currently charging at home" },
            { toolTipTextKeys.CarAtHome, "Your car is in your defined GeoFence" },
            { toolTipTextKeys.CarNotHealthy, "Your car has no optimal internet connection or there is an issue with the Tesla API." },
            { toolTipTextKeys.CarPluggedIn, "Your car is plugged in" },
            { toolTipTextKeys.CarChargeMode, "ChargeMode of your car. Click <a href=\"https://github.com/pkuehnel/TeslaSolarCharger#charge-modes\"  target=\"_blank\">here</a> for details."},
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

        var carState = settings.Cars.First(c => c.Id == carId);

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

            var propertyValue = property.GetValue(carState, null);

            if (property.PropertyType == typeof(List<DateTime>))
            {
                var dateList = (List<DateTime>?)propertyValue;
                var currentDate = dateTimeProvider.UtcNow().Date;
                nonDateValues.Add(new DtoCarTopicValue()
                {
                    Topic = AddSpacesBeforeCapitalLetters(property.Name),
                    Value = dateList?
                        .Where(d => d > currentDate)
                        .Count()
                        .ToString(),
                });
            }
            else if (
                property.PropertyType == typeof(DateTimeOffset?)
                || property.PropertyType == typeof(DateTimeOffset)
            )
            {
                dateValues.Add(new DtoCarDateTopics()
                {
                    Topic = AddSpacesBeforeCapitalLetters(property.Name),
                    DateTime = ((DateTimeOffset?)propertyValue)?.LocalDateTime,
                });
            }
            else if (
                property.PropertyType == typeof(DateTime?)
                || property.PropertyType == typeof(DateTime)
            )
            {
                dateValues.Add(new DtoCarDateTopics()
                {
                    Topic = AddSpacesBeforeCapitalLetters(property.Name),
                    DateTime = (DateTime?)propertyValue,
                });
            }
            else if (
                property.PropertyType.IsGenericType
                && property.PropertyType.GetGenericTypeDefinition() == typeof(DtoTimeStampedValue<>)
            )
            {
                // Serialize entire DtoTimeStampedValue<T> as JSON
                var timeStampedValueObject = propertyValue;
                var jsonString = JsonSerializer.Serialize(timeStampedValueObject);

                nonDateValues.Add(new DtoCarTopicValue()
                {
                    Topic = AddSpacesBeforeCapitalLetters(property.Name),
                    Value = jsonString,
                });
            }
            else
            {
                nonDateValues.Add(new DtoCarTopicValue()
                {
                    Topic = AddSpacesBeforeCapitalLetters(property.Name),
                    Value = propertyValue?.ToString(),
                });
            }
        }

        return dtoCarTopicValues;
    }
    public List<DtoChargingSlot> RecalculateAndGetChargingSlots(int carId)
    {
        logger.LogTrace("{method}({carId})", nameof(RecalculateAndGetChargingSlots), carId);
        var car = settings.Cars.First(c => c.Id == carId);
        chargeTimeCalculationService.UpdatePlannedChargingSlots(car);
        return car.PlannedChargingSlots;
    }

    public List<DtoChargingSlot> GetChargingSlots(int carId)
    {
        logger.LogTrace("{method}({carId})", nameof(GetChargingSlots), carId);
        var car = settings.Cars.First(c => c.Id == carId);
        return car.PlannedChargingSlots;
    }

    public async Task UpdateCarFleetApiState(int carId, TeslaCarFleetApiState fleetApiState)
    {
        logger.LogTrace("{method}({carId}, {fleetApiState})", nameof(UpdateCarFleetApiState), carId, fleetApiState);
        var car = teslaSolarChargerContext.Cars.First(c => c.Id == carId);
        car.TeslaFleetApiState = fleetApiState;
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
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
        logger.LogTrace("{method}()", nameof(GetEnabledCars));
        return settings.CarsToManage;
    }

    

    public async Task<string?> GetVinByCarId(int carId)
    {
        logger.LogTrace("{method}({carId})", nameof(GetVinByCarId), carId);
        return await teslaSolarChargerContext.Cars
            .Where(c => c.Id == carId)
            .Select(c => c.Vin).FirstAsync().ConfigureAwait(false);
    }
}
