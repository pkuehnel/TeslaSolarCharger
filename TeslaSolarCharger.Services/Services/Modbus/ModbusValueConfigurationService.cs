using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Services.Services.Modbus.Contracts;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;

namespace TeslaSolarCharger.Services.Services.Modbus;

public class ModbusValueConfigurationService (
    ILogger<ModbusValueConfigurationService> logger,
    ITeslaSolarChargerContext context,
    IModbusClientHandlingService modbusClientHandlingService) : IModbusValueConfigurationService
{
    public async Task<List<DtoModbusConfiguration>> GetModbusConfigurationByPredicate(Expression<Func<ModbusConfiguration, bool>> predicate)
    {
        logger.LogTrace("{method}({predicate})", nameof(GetModbusConfigurationByPredicate), predicate);
        var resultConfigurations = await context.ModbusConfigurations
            .Where(predicate)
            .Select(c => new DtoModbusConfiguration()
            {
                Id = c.Id,
                UnitIdentifier = c.UnitIdentifier,
                Host = c.Host,
                Port = c.Port,
                Endianess = c.Endianess,
                ConnectDelayMilliseconds = c.ConnectDelayMilliseconds,
                ReadTimeoutMilliseconds = c.ReadTimeoutMilliseconds
            })
            .ToListAsync().ConfigureAwait(false);
        return resultConfigurations;
    }

    public async Task<DtoModbusConfiguration> GetValueConfigurationById(int id)
    {
        logger.LogTrace("{method}({id})", nameof(GetValueConfigurationById), id);
        var configurations = await GetModbusConfigurationByPredicate(x => x.Id == id);
        return configurations.Single();
    }

    public async Task<List<DtoModbusValueResultConfiguration>> GetModbusResultConfigurationsByPredicate(
        Expression<Func<ModbusResultConfiguration, bool>> predicate)
    {
        logger.LogTrace("{method}({predicate})", nameof(GetModbusResultConfigurationsByPredicate), predicate);
        var resultConfigurations = await context.ModbusResultConfigurations
            .Where(predicate)
            .Select(e => new DtoModbusValueResultConfiguration()
            {
                Id = e.Id,
                CorrectionFactor = e.CorrectionFactor,
                UsedFor = e.UsedFor,
                Operator = e.Operator,
                RegisterType = e.RegisterType,
                ValueType = e.ValueType,
                Address = e.Address,
                Length = e.Length,
                BitStartIndex = e.BitStartIndex,
                InvertedByModbusResultConfigurationId = e.InvertedByModbusResultConfigurationId,
            })
            .ToListAsync().ConfigureAwait(false);
        return resultConfigurations;
    }

    public async Task<List<DtoModbusValueResultConfiguration>> GetResultConfigurationsByValueConfigurationId(int valueId)
    {
        logger.LogTrace("{method}({id})", nameof(GetResultConfigurationsByValueConfigurationId), valueId);
        var resultConfigurations = await GetModbusResultConfigurationsByPredicate(x => x.ModbusConfigurationId == valueId);
        return resultConfigurations;
    }

    public async Task<int> SaveModbusResultConfiguration(int parentId, DtoModbusValueResultConfiguration dtoData)
    {
        logger.LogTrace("{method}({parentId}, {@dtoData})", nameof(SaveModbusResultConfiguration), parentId, dtoData);
        var dbData = new ModbusResultConfiguration()
        {
            Id = dtoData.Id,
            CorrectionFactor = dtoData.CorrectionFactor,
            UsedFor = dtoData.UsedFor,
            Operator = dtoData.Operator,
            RegisterType = dtoData.RegisterType,
            ValueType = dtoData.ValueType,
            Address = dtoData.Address,
            Length = dtoData.Length,
            BitStartIndex = dtoData.BitStartIndex,
            ModbusConfigurationId = parentId,
            InvertedByModbusResultConfigurationId = dtoData.InvertedByModbusResultConfigurationId,
        };
        dbData.ModbusConfigurationId = parentId;
        var trackedData = context.ChangeTracker.Entries<ModbusResultConfiguration>()
            .FirstOrDefault(e => e.Entity.Id == dbData.Id);
        if (trackedData == default)
        {
            if (dbData.Id == default)
            {
                context.ModbusResultConfigurations.Add(dbData);
            }
            else
            {
                context.ModbusResultConfigurations.Update(dbData);
            }
        }
        else
        {
            trackedData.CurrentValues.SetValues(dbData);
        }
        await context.SaveChangesAsync().ConfigureAwait(false);
        return dbData.Id;
    }

    public async Task DeleteModbusConfiguration(int id)
    {
        logger.LogTrace("{method}({id})", nameof(DeleteModbusConfiguration), id);
        var modbusConfiguration = await context.ModbusConfigurations
            .Include(m => m.ModbusResultConfigurations)
            .FirstAsync(x => x.Id == id).ConfigureAwait(false);
        context.ModbusConfigurations.Remove(modbusConfiguration);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task DeleteResultConfiguration(int id)
    {
        logger.LogTrace("{method}({id})", nameof(DeleteResultConfiguration), id);
        var modbusResultConfiguration = await context.ModbusResultConfigurations
            .FirstAsync(x => x.Id == id).ConfigureAwait(false);
        context.ModbusResultConfigurations.Remove(modbusResultConfiguration);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<int> SaveModbusConfiguration(DtoModbusConfiguration dtoData)
    {
        logger.LogTrace("{method}({@dtoData})", nameof(SaveModbusConfiguration), dtoData);
        if (dtoData.Id != default)
        {
            modbusClientHandlingService.RemoveClient(dtoData.Host, dtoData.Port);
            var hostPortCombination = context.ModbusConfigurations.Where(x => x.Id == dtoData.Id)
                .Select(x => new { x.Host, x.Port })
                .Single();
            modbusClientHandlingService.RemoveClient(hostPortCombination.Host, hostPortCombination.Port);
        }

        var dbData = new ModbusConfiguration()
        {
            Id = dtoData.Id,
            UnitIdentifier = dtoData.UnitIdentifier ?? 0,
            Host = dtoData.Host,
            Port = dtoData.Port,
            Endianess = dtoData.Endianess,
            ConnectDelayMilliseconds = dtoData.ConnectDelayMilliseconds,
            ReadTimeoutMilliseconds = dtoData.ReadTimeoutMilliseconds
        };
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
