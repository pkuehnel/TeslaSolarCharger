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
}
