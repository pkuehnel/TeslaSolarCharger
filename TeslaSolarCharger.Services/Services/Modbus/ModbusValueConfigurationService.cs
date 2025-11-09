using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Services.Services.Modbus.Contracts;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Services.Services.ValueRefresh;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Services.Services.Modbus;

public class ModbusValueConfigurationService (
    ILogger<ModbusValueConfigurationService> logger,
    ITeslaSolarChargerContext context,
    IModbusClientHandlingService modbusClientHandlingService,
    IRefreshableValueHandlingService refreshableValueHandlingService,
    IServiceScopeFactory serviceScopeFactory,
    IConstants constants) : IModbusValueConfigurationService, IRefreshableValueSetupService
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
                ReadTimeoutMilliseconds = c.ReadTimeoutMilliseconds,
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
        await refreshableValueHandlingService.RecreateRefreshables().ConfigureAwait(false);
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
        await refreshableValueHandlingService.RecreateRefreshables().ConfigureAwait(false);
    }

    public async Task DeleteResultConfiguration(int id)
    {
        logger.LogTrace("{method}({id})", nameof(DeleteResultConfiguration), id);
        var modbusResultConfiguration = await context.ModbusResultConfigurations
            .FirstAsync(x => x.Id == id).ConfigureAwait(false);
        context.ModbusResultConfigurations.Remove(modbusResultConfiguration);
        await context.SaveChangesAsync().ConfigureAwait(false);
        await refreshableValueHandlingService.RecreateRefreshables().ConfigureAwait(false);
    }

    public async Task<int> SaveModbusConfiguration(DtoModbusConfiguration dtoData)
    {
        logger.LogTrace("{method}({@dtoData})", nameof(SaveModbusConfiguration), dtoData);
        if (dtoData.Id != default)
        {
            await modbusClientHandlingService.RemoveClient(dtoData.Host, dtoData.Port);
            var hostPortCombination = context.ModbusConfigurations.Where(x => x.Id == dtoData.Id)
                .Select(x => new { x.Host, x.Port })
                .Single();
            await modbusClientHandlingService.RemoveClient(hostPortCombination.Host, hostPortCombination.Port);
        }

        var dbData = new ModbusConfiguration()
        {
            Id = dtoData.Id,
            UnitIdentifier = dtoData.UnitIdentifier ?? 0,
            Host = dtoData.Host,
            Port = dtoData.Port,
            Endianess = dtoData.Endianess,
            ConnectDelayMilliseconds = dtoData.ConnectDelayMilliseconds,
            ReadTimeoutMilliseconds = dtoData.ReadTimeoutMilliseconds,
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
        await refreshableValueHandlingService.RecreateRefreshables().ConfigureAwait(false);
        return dbData.Id;
    }

    public async Task<List<DelegateRefreshableValue<decimal>>> GetDecimalRefreshableValuesAsync(TimeSpan defaultInterval)
    {
        var modbusConfigurations = await GetModbusConfigurationByPredicate(
                c => true).ConfigureAwait(false);

        var result = new List<DelegateRefreshableValue<decimal>>();
        foreach (var modbusConfiguration in modbusConfigurations)
        {
            try
            {
                var configuration = modbusConfiguration;
                var refreshable = new DelegateRefreshableValue<decimal>(
                    serviceScopeFactory,
                    async ct =>
                    {
                        using var executionScope = serviceScopeFactory.CreateScope();
                        var modbusValueConfigurationService = executionScope.ServiceProvider
                            .GetRequiredService<IModbusValueConfigurationService>();
                        var modbusValueExecutionService = executionScope.ServiceProvider
                            .GetRequiredService<IModbusValueExecutionService>();

                        var resultConfigurations = await modbusValueConfigurationService
                            .GetResultConfigurationsByValueConfigurationId(configuration.Id)
                            .ConfigureAwait(false);

                        var values = new Dictionary<ValueKey, ConcurrentDictionary<int, decimal>>();
                        foreach (var resultConfiguration in resultConfigurations)
                        {
                            ct.ThrowIfCancellationRequested();
                            var valueKey = new ValueKey(configuration.Id, ConfigurationType.ModbusSolarValue, resultConfiguration.UsedFor, null);
                            try
                            {
                                var byteArray = await modbusValueExecutionService
                                    .GetResult(configuration, resultConfiguration, false)
                                    .ConfigureAwait(false);
                                var value = await modbusValueExecutionService
                                    .GetValue(byteArray, resultConfiguration)
                                    .ConfigureAwait(false);

                                if (!values.TryGetValue(valueKey, out var current))
                                {
                                    current = new();
                                    values[valueKey] = current;
                                }
                                current.TryAdd(resultConfiguration.Id, value);

                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(
                                    ex,
                                    "Error while refreshing modbus value for configuration {configurationId} result {resultId}",
                                    configuration.Id,
                                    resultConfiguration.Id);
                                throw;
                            }
                        }

                        return new ReadOnlyDictionary<ValueKey, ConcurrentDictionary<int, decimal>>(values);
                    },
                    defaultInterval,
                    constants.SolarHistoricValueCapacity
                );

                result.Add(refreshable);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error while creating refreshable for modbus configuration {configurationId} ({host}:{port})",
                    modbusConfiguration.Id,
                    modbusConfiguration.Host,
                    modbusConfiguration.Port);
            }
        }

        return result;
    }
}
