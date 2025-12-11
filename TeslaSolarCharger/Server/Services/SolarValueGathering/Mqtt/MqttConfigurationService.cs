using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Mqtt.Contracts;
using TeslaSolarCharger.Shared.Dtos.MqttConfiguration;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Mqtt;

public class MqttConfigurationService(ILogger<MqttConfigurationService> logger,
    ITeslaSolarChargerContext context) : IMqttConfigurationService
{
    public async Task<List<DtoMqttConfiguration>> GetMqttConfigurationsByPredicate(Expression<Func<MqttConfiguration, bool>> predicate)
    {
        logger.LogTrace("{method}({predicate})", nameof(GetMqttConfigurationsByPredicate), predicate);
        var resultConfigurations = await context.MqttConfigurations
            .Where(predicate)
            .Select(e => new DtoMqttConfiguration()
            {
                Id = e.Id,
                Host = e.Host,
                Port = e.Port,
                Username = e.Username,
                Password = e.Password,
            })
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

        var dbData = new MqttConfiguration()
        {
            Id = dtoData.Id,
            Host = dtoData.Host,
            Port = dtoData.Port,
            Username = dtoData.Username,
            Password = dtoData.Password,
        };
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
        var resultConfigurations = await context.MqttResultConfigurations
            .Where(predicate)
            .Select(e => new DtoMqttResultConfiguration()
            {
                Id = e.Id,
                CorrectionFactor = e.CorrectionFactor,
                UsedFor = e.UsedFor,
                Operator = e.Operator,
                NodePattern = e.NodePattern,
                XmlAttributeHeaderName = e.XmlAttributeHeaderName,
                XmlAttributeHeaderValue = e.XmlAttributeHeaderValue,
                XmlAttributeValueName = e.XmlAttributeValueName,
                Topic = e.Topic,
                NodePatternType = e.NodePatternType,
            })
            .ToListAsync().ConfigureAwait(false);
        return resultConfigurations;
    }

    public async Task<List<DtoMqttResultConfiguration>> GetResultConfigurationsByParentId(int parentId)
    {
        logger.LogTrace("{method}({parentId})", nameof(GetResultConfigurationsByParentId), parentId);
        var configurations = await GetMqttResultConfigurationsByPredicate(x => x.MqttConfigurationId == parentId);
        return configurations;
    }

    public async Task<int> SaveResultConfiguration(int parentId, DtoMqttResultConfiguration dtoData)
    {
        logger.LogTrace("{method}({@dtoData})", nameof(SaveResultConfiguration), dtoData);
        var dbData = new MqttResultConfiguration
        {
            Id = dtoData.Id,
            CorrectionFactor = dtoData.CorrectionFactor,
            UsedFor = dtoData.UsedFor,
            Operator = dtoData.Operator,
            NodePattern = dtoData.NodePattern,
            XmlAttributeHeaderName = dtoData.XmlAttributeHeaderName,
            XmlAttributeHeaderValue = dtoData.XmlAttributeHeaderValue,
            XmlAttributeValueName = dtoData.XmlAttributeValueName,
            Topic = dtoData.Topic,
            NodePatternType = dtoData.NodePatternType,
            MqttConfigurationId = parentId,
        };
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
