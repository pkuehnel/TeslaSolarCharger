using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.MappingExtensions;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services;

public class ConfigService : IConfigService
{
    private readonly ILogger<ConfigService> _logger;
    private readonly ISettings _settings;
    private readonly IIndexService _indexService;
    private readonly IConfigJsonService _configJsonService;
    private readonly IMapperConfigurationFactory _mapperConfigurationFactory;
    private readonly ITeslaSolarChargerContext _teslaSolarChargerContext;

    public ConfigService(ILogger<ConfigService> logger,
        ISettings settings, IIndexService indexService, IConfigJsonService configJsonService,
        IMapperConfigurationFactory mapperConfigurationFactory,
        ITeslaSolarChargerContext teslaSolarChargerContext)
    {
        _logger = logger;
        _settings = settings;
        _indexService = indexService;
        _configJsonService = configJsonService;
        _mapperConfigurationFactory = mapperConfigurationFactory;
        _teslaSolarChargerContext = teslaSolarChargerContext;
    }

    public ISettings GetSettings()
    {
        _logger.LogTrace("{method}()", nameof(GetSettings));
        return _settings;
    }

    public async Task<List<CarBasicConfiguration>> GetCarBasicConfigurations()
    {
        _logger.LogTrace("{method}()", nameof(GetCarBasicConfigurations));

        var mapper = _mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<Car, CarBasicConfiguration>()
                ;
        });

        var cars = await _teslaSolarChargerContext.Cars
            .ProjectTo<CarBasicConfiguration>(mapper)
            .ToListAsync().ConfigureAwait(false);

        return cars;
    }
}
