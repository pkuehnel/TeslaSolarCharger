using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Services.Services.Modbus;
using TeslaSolarCharger.Services.Services.Mqtt.Contracts;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.Shared.Dtos.MqttConfiguration;
using TeslaSolarCharger.SharedBackend.MappingExtensions;

namespace TeslaSolarCharger.Services.Services.Mqtt;

public class MqttConfigurationService(ILogger<MqttConfigurationService> logger,
    ITeslaSolarChargerContext context,
    IMapperConfigurationFactory mapperConfigurationFactory) : IMqttConfigurationService
{
    public async Task<List<DtoMqttConfiguration>> GetMqttConfigurationsByPredicate(Expression<Func<MqttConfiguration, bool>> predicate)
    {
        logger.LogTrace("{method}({predicate})", nameof(GetMqttConfigurationsByPredicate), predicate);
        var mapper = mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<MqttConfiguration, DtoMqttConfiguration>()
                ;
        });
        var resultConfigurations = await context.MqttConfigurations
            .Where(predicate)
            .ProjectTo<DtoMqttConfiguration>(mapper)
            .ToListAsync().ConfigureAwait(false);
        return resultConfigurations;
    }

    public async Task<DtoMqttConfiguration> GetConfigurationById(int id)
    {
        logger.LogTrace("{method}({id})", nameof(GetConfigurationById), id);
        var configurations = await GetMqttConfigurationsByPredicate(x => x.Id == id);
        return configurations.Single();
    }

    public async Task<int> SaveConfiguration(DtoMqttConfiguration dtoData)
    {
        logger.LogTrace("{method}({@dtoData})", nameof(SaveConfiguration), dtoData);
        var mapperConfiguration = mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<DtoMqttConfiguration, MqttConfiguration>()
                ;
        });
        if (dtoData.Id != default)
        {
            //ToDo: handle client reconnection
            //modbusClientHandlingService.RemoveClient(dtoData.Host, dtoData.Port);
            //var hostPortCombination = context.ModbusConfigurations.Where(x => x.Id == dtoData.Id)
            //.Select(x => new { x.Host, x.Port })
            //    .Single();
            //modbusClientHandlingService.RemoveClient(hostPortCombination.Host, hostPortCombination.Port);
        }

        var mapper = mapperConfiguration.CreateMapper();
        var dbData = mapper.Map<MqttConfiguration>(dtoData);
        if (dbData.Id == default)
        {
            context.MqttConfigurations.Add(dbData);
        }
        else
        {
            context.MqttConfigurations.Update(dbData);
        }
        await context.SaveChangesAsync().ConfigureAwait(false);
        return dbData.Id;
    }

    public async Task DeleteConfiguration(int id)
    {
        logger.LogTrace("{method}({id})", nameof(DeleteConfiguration), id);
        var configuration = await context.MqttConfigurations
            .Include(m => m.MqttResultConfigurations)
            .FirstAsync(x => x.Id == id).ConfigureAwait(false);
        context.MqttConfigurations.Remove(configuration);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<List<DtoMqttResultConfiguration>> GetMqttResultConfigurationsByPredicate(Expression<Func<MqttResultConfiguration, bool>> predicate)
    {
        logger.LogTrace("{method}({predicate})", nameof(GetMqttResultConfigurationsByPredicate), predicate);
        var mapper = mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<MqttResultConfiguration, DtoMqttResultConfiguration>()
                ;
        });
        var resultConfigurations = await context.MqttResultConfigurations
            .Where(predicate)
            .ProjectTo<DtoMqttResultConfiguration>(mapper)
            .ToListAsync().ConfigureAwait(false);
        return resultConfigurations;
    }

    public async Task<DtoMqttResultConfiguration> GetResultConfigurationById(int id)
    {
        logger.LogTrace("{method}({id})", nameof(GetResultConfigurationById), id);
        var configurations = await GetMqttResultConfigurationsByPredicate(x => x.Id == id);
        return configurations.Single();
    }

    public async Task<int> SaveResultConfiguration(int parentId, DtoMqttResultConfiguration dtoData)
    {
        logger.LogTrace("{method}({@dtoData})", nameof(SaveResultConfiguration), dtoData);
        var mapperConfiguration = mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<DtoMqttResultConfiguration, MqttResultConfiguration>()
                ;
        });
        var mapper = mapperConfiguration.CreateMapper();
        var dbData = mapper.Map<MqttResultConfiguration>(dtoData);
        dbData.MqttConfigurationId = parentId;
        if (dbData.Id == default)
        {
            context.MqttResultConfigurations.Add(dbData);
        }
        else
        {
            context.MqttResultConfigurations.Update(dbData);
        }
        await context.SaveChangesAsync().ConfigureAwait(false);
        return dbData.Id;
    }

    public async Task DeleteResultConfiguration(int id)
    {
        logger.LogTrace("{method}({id})", nameof(DeleteResultConfiguration), id);
        var configuration = await context.MqttResultConfigurations
            .FirstAsync(x => x.Id == id).ConfigureAwait(false);
        context.MqttResultConfigurations.Remove(configuration);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }
}
