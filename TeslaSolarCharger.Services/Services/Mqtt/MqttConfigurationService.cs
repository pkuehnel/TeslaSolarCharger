using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Configuration;
using System.Linq.Expressions;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Services.Services.Modbus;
using TeslaSolarCharger.Services.Services.Mqtt.Contracts;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.Shared.Dtos.MqttConfiguration;

namespace TeslaSolarCharger.Services.Services.Mqtt;

public class MqttConfigurationService(ILogger<MqttConfigurationService> logger,
    ITeslaSolarChargerContext context,
    IMqttClientHandlingService mqttClientHandlingService) : IMqttConfigurationService
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
        if (dtoData.Id != default)
        {
            mqttClientHandlingService.RemoveClient(dtoData.Host, dtoData.Port, dtoData.Username);
            RemoveMqttClientsByConfigurationId(dtoData.Id);
        }

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
        await ConnectMqttClientByConfigurationId(dbData.Id);
        return dbData.Id;
    }

    

    public async Task DeleteConfiguration(int id)
    {
        logger.LogTrace("{method}({id})", nameof(DeleteConfiguration), id);
        var configuration = await context.MqttConfigurations
            .Include(m => m.MqttResultConfigurations)
            .FirstAsync(x => x.Id == id).ConfigureAwait(false);
        context.MqttConfigurations.Remove(configuration);
        RemoveMqttClientsByConfigurationId(configuration.Id);
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
        RemoveMqttClientsByConfigurationId(parentId);
        await context.SaveChangesAsync().ConfigureAwait(false);
        await ConnectMqttClientByConfigurationId(parentId);
        return dbData.Id;
    }

    public async Task DeleteResultConfiguration(int id)
    {
        logger.LogTrace("{method}({id})", nameof(DeleteResultConfiguration), id);
        var configuration = await context.MqttResultConfigurations
            .FirstAsync(x => x.Id == id).ConfigureAwait(false);
        context.MqttResultConfigurations.Remove(configuration);
        RemoveMqttClientsByConfigurationId(configuration.MqttConfigurationId);
        await context.SaveChangesAsync().ConfigureAwait(false);
        await ConnectMqttClientByConfigurationId(configuration.MqttConfigurationId);
    }

    private void RemoveMqttClientsByConfigurationId(int id)
    {
        var hostPortUserCombination = context.MqttConfigurations.Where(x => x.Id == id)
            .Select(x => new { x.Host, x.Port, x.Username })
            .Single();
        mqttClientHandlingService.RemoveClient(hostPortUserCombination.Host, hostPortUserCombination.Port, hostPortUserCombination.Username);
    }

    private async Task ConnectMqttClientByConfigurationId(int configurationId)
    {
        logger.LogTrace("{method}({configurationId})", nameof(ConnectMqttClientByConfigurationId), configurationId);
        var configuration = await GetConfigurationById(configurationId);
        var resultConfigurations = await GetMqttResultConfigurationsByPredicate(x => x.MqttConfigurationId == configurationId);
        await mqttClientHandlingService.ConnectClient(configuration, resultConfigurations, true);
    }
}
