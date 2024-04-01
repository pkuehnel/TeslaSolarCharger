using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;
using TeslaSolarCharger.SharedBackend.MappingExtensions;

namespace TeslaSolarCharger.Services.Services;

public class ModbusValueConfigurationService (
    ILogger<ModbusValueConfigurationService> logger,
    ITeslaSolarChargerContext context,
    IMapperConfigurationFactory mapperConfigurationFactory) : IModbusValueConfigurationService
{
    public async Task<List<DtoModbusConfiguration>> GetModbusConfigurationByPredicate(Expression<Func<ModbusConfiguration, bool>> predicate)
    {
        logger.LogTrace("{method}({predicate})", nameof(GetModbusConfigurationByPredicate), predicate);
        var mapper = mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<ModbusConfiguration, DtoModbusConfiguration>()
                ;
        });
        var resultConfigurations = await context.ModbusConfigurations
            .Where(predicate)
            .ProjectTo<DtoModbusConfiguration>(mapper)
            .ToListAsync().ConfigureAwait(false);
        return resultConfigurations;
    }

    public async Task<int> SaveModbusConfiguration(DtoModbusConfiguration dtoData)
    {
        logger.LogTrace("{method}({@dtoData})", nameof(SaveModbusConfiguration), dtoData);
        var mapperConfiguration = mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<DtoModbusConfiguration, ModbusConfiguration>()
                ;
        });

        var mapper = mapperConfiguration.CreateMapper();
        var dbData = mapper.Map<ModbusConfiguration>(dtoData);
        if (dbData.Id == default)
        {
            context.ModbusConfigurations.Add(dbData);
        }
        else
        {
            context.ModbusConfigurations.Update(dbData);
        }
        await context.SaveChangesAsync().ConfigureAwait(false);
        return dbData.Id;
    }
}
