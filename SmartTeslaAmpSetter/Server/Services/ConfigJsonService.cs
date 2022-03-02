using System.Reflection;
using Newtonsoft.Json;
using SmartTeslaAmpSetter.Shared;
using SmartTeslaAmpSetter.Shared.Dtos;
using SmartTeslaAmpSetter.Shared.Enums;

namespace SmartTeslaAmpSetter.Server.Services;

public class ConfigJsonService
{
    private readonly ILogger<ConfigJsonService> _logger;
    private readonly IConfiguration _configuration;
    private readonly Settings _settings;

    public ConfigJsonService(ILogger<ConfigJsonService> logger, IConfiguration configuration, Settings settings)
    {
        _logger = logger;
        _configuration = configuration;
        _settings = settings;
    }

    public bool CarConfigurationFileExists()
    {
        var path = GetConfigurationFileFullPath();
        return File.Exists(path);
    }

    public string GetConfigurationFileFullPath()
    {
        var configFileLocation = _configuration.GetValue<string>("ConfigFileLocation");
        var path = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory?.FullName;
        path = Path.Combine(path ?? throw new InvalidOperationException("Could not get Assembly directory"), configFileLocation);
        return path;
    }

    public async Task<List<Car>> GetCarsFromConfiguration()
    {
        var cars = new List<Car>();
        if (CarConfigurationFileExists())
        {
            try
            {
                var fileContent = await File.ReadAllTextAsync(GetConfigurationFileFullPath()).ConfigureAwait(false);
                cars = JsonConvert.DeserializeObject<List<Car>>(fileContent) ?? throw new InvalidOperationException();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get car configurations, use default configuration");
            }
        }

        foreach (var car in cars)
        {
            car.CarState.ShouldStopChargingSince = DateTime.MaxValue;
            car.CarState.ShouldStartChargingSince = DateTime.MaxValue;
        }

        var carIds = _configuration.GetValue<string>("CarPriorities").Split("|");
        RemoveOldCars(cars, carIds);
        
        var newCarIds = carIds.Where(i => !cars.Any(c => c.Id.ToString().Equals(i))).ToList();
        AddNewCars(newCarIds, cars);

        return cars;
    }

    private static void AddNewCars(List<string> newCarIds, List<Car> cars)
    {
        foreach (var carId in newCarIds)
        {
            var id = int.Parse(carId);
            if (cars.All(c => c.Id != id))
            {
                var car = new Car
                {
                    Id = id,
                    CarConfiguration =
                    {
                        ChargeMode = ChargeMode.MaxPower,
                        UpdatedSincLastWrite = true,
                    },
                    CarState =
                    {
                        ShouldStartChargingSince = DateTime.MaxValue,
                        ShouldStopChargingSince = DateTime.MaxValue,
                    },
                };
                cars.Add(car);
            }
        }
    }

    public async Task UpdateConfigJson()
    {
        _logger.LogTrace("{method}()", nameof(UpdateConfigJson));
        var configFileLocation = GetConfigurationFileFullPath();
        if (_settings.Cars.Any(c => c.CarConfiguration.UpdatedSincLastWrite))
        {
            _logger.LogDebug("Update configuration.json");
            var fileInfo = new FileInfo(configFileLocation);
            if (!Directory.Exists(fileInfo.Directory?.FullName))
            {
                Directory.CreateDirectory(fileInfo.Directory?.FullName ?? throw new InvalidOperationException());
            }

            var settings = new JsonSerializerSettings()
            {
                ContractResolver = new ConfigPropertyResolver()
            };
            var json = JsonConvert.SerializeObject(_settings.Cars, settings);
            await File.WriteAllTextAsync(configFileLocation, json);

            foreach (var settingsCar in _settings.Cars)
            {
                settingsCar.CarConfiguration.UpdatedSincLastWrite = false;
            }
        }
    }

    private void RemoveOldCars(List<Car> cars, string[] carIds)
    {
        foreach (var car in cars)
        {
            if (!carIds.Any(c => c.Equals(car.Id.ToString())))
            {
                cars.RemoveAll(c => c.Id == car.Id);
            }
        }
    }
}