using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.MappingExtensions;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class TeslaFleetApiService : ITeslaService
{
    private readonly ILogger<TeslaFleetApiService> _logger;
    private readonly ITeslaSolarChargerContext _context;
    private readonly IMapperConfigurationFactory _mapperConfigurationFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public TeslaFleetApiService(ILogger<TeslaFleetApiService> logger, ITeslaSolarChargerContext context,
        IMapperConfigurationFactory mapperConfigurationFactory, IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _context = context;
        _mapperConfigurationFactory = mapperConfigurationFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task StartCharging(int carId, int startAmp, CarStateEnum? carState)
    {
        _logger.LogTrace("{method}({carId}, {startAmp}, {carState})", nameof(StartCharging), carId, startAmp, carState);
        var token = await GetTeslaTokenAsync().ConfigureAwait(false);
        throw new NotImplementedException();
    }

    public Task WakeUpCar(int carId)
    {
        throw new NotImplementedException();
    }

    public Task StopCharging(int carId)
    {
        throw new NotImplementedException();
    }

    public Task SetAmp(int carId, int amps)
    {
        throw new NotImplementedException();
    }

    public Task SetScheduledCharging(int carId, DateTimeOffset? chargingStartTime)
    {
        throw new NotImplementedException();
    }

    private async Task<DtoTeslaToken> GetTeslaTokenAsync()
    {
        var mapper = _mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<TeslaToken, DtoTeslaToken>()
                ;
        });
        var currentDateTime = _dateTimeProvider.UtcNow();
        var token = await _context.TeslaTokens
            .Where(t => t.ExpiresAtUtc > currentDateTime)
            .OrderByDescending(t => t.ExpiresAtUtc)
            .ProjectTo<DtoTeslaToken>(mapper)
            .FirstAsync().ConfigureAwait(false);
        return token;
    }
}
