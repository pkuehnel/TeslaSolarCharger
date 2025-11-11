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

public class ModbusValueConfigurationService : IModbusValueConfigurationService, IRefreshableValueSetupService
{
    private readonly ILogger<ModbusValueConfigurationService> _logger;
    private readonly ITeslaSolarChargerContext _context;
    private readonly IModbusClientHandlingService _modbusClientHandlingService;
    private readonly IRefreshableValueHandlingService _refreshableValueHandlingService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConstants _constants;

    public ModbusValueConfigurationService(ILogger<ModbusValueConfigurationService> logger,
        ITeslaSolarChargerContext context,
        IModbusClientHandlingService modbusClientHandlingService,
        IRefreshableValueHandlingService refreshableValueHandlingService,
        IServiceScopeFactory serviceScopeFactory,
        IConstants constants)
    {
        _logger = logger;
        _context = context;
        _modbusClientHandlingService = modbusClientHandlingService;
        _refreshableValueHandlingService = refreshableValueHandlingService;
        _serviceScopeFactory = serviceScopeFactory;
        _constants = constants;
    }

    public async Task<List<DtoModbusConfiguration>> GetModbusConfigurationByPredicate(Expression<Func<ModbusConfiguration, bool>> predicate)
    {
        _logger.LogTrace("{method}({predicate})", nameof(GetModbusConfigurationByPredicate), predicate);
        var resultConfigurations = await _context.ModbusConfigurations
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
        _logger.LogTrace("{method}({id})", nameof(GetValueConfigurationById), id);
        var configurations = await GetModbusConfigurationByPredicate(x => x.Id == id);
        return configurations.Single();
    }

    public async Task<List<DtoModbusValueResultConfiguration>> GetModbusResultConfigurationsByPredicate(
        Expression<Func<ModbusResultConfiguration, bool>> predicate)
    {
        _logger.LogTrace("{method}({predicate})", nameof(GetModbusResultConfigurationsByPredicate), predicate);
        var resultConfigurations = await _context.ModbusResultConfigurations
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
        _logger.LogTrace("{method}({id})", nameof(GetResultConfigurationsByValueConfigurationId), valueId);
        var resultConfigurations = await GetModbusResultConfigurationsByPredicate(x => x.ModbusConfigurationId == valueId);
        return resultConfigurations;
    }

    public async Task<int> SaveModbusResultConfiguration(int parentId, DtoModbusValueResultConfiguration dtoData)
    {
        _logger.LogTrace("{method}({parentId}, {@dtoData})", nameof(SaveModbusResultConfiguration), parentId, dtoData);
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
        var trackedData = _context.ChangeTracker.Entries<ModbusResultConfiguration>()
            .FirstOrDefault(e => e.Entity.Id == dbData.Id);
        if (trackedData == default)
        {
            if (dbData.Id == default)
            {
                _context.ModbusResultConfigurations.Add(dbData);
            }
            else
            {
                _context.ModbusResultConfigurations.Update(dbData);
            }
        }
        else
        {
            trackedData.CurrentValues.SetValues(dbData);
        }
        await _context.SaveChangesAsync().ConfigureAwait(false);
        await _refreshableValueHandlingService.RecreateRefreshables().ConfigureAwait(false);
        return dbData.Id;
    }

    public async Task DeleteModbusConfiguration(int id)
    {
        _logger.LogTrace("{method}({id})", nameof(DeleteModbusConfiguration), id);
        var modbusConfiguration = await _context.ModbusConfigurations
            .Include(m => m.ModbusResultConfigurations)
            .FirstAsync(x => x.Id == id).ConfigureAwait(false);
        _context.ModbusConfigurations.Remove(modbusConfiguration);
        await _context.SaveChangesAsync().ConfigureAwait(false);
        await _refreshableValueHandlingService.RecreateRefreshables().ConfigureAwait(false);
    }

    public async Task DeleteResultConfiguration(int id)
    {
        _logger.LogTrace("{method}({id})", nameof(DeleteResultConfiguration), id);
        var modbusResultConfiguration = await _context.ModbusResultConfigurations
            .FirstAsync(x => x.Id == id).ConfigureAwait(false);
        _context.ModbusResultConfigurations.Remove(modbusResultConfiguration);
        await _context.SaveChangesAsync().ConfigureAwait(false);
        await _refreshableValueHandlingService.RecreateRefreshables().ConfigureAwait(false);
    }

    public async Task<int> SaveModbusConfiguration(DtoModbusConfiguration dtoData)
    {
        _logger.LogTrace("{method}({@dtoData})", nameof(SaveModbusConfiguration), dtoData);
        if (dtoData.Id != default)
        {
            await _modbusClientHandlingService.RemoveClient(dtoData.Host, dtoData.Port);
            var hostPortCombination = _context.ModbusConfigurations.Where(x => x.Id == dtoData.Id)
                .Select(x => new { x.Host, x.Port })
                .Single();
            await _modbusClientHandlingService.RemoveClient(hostPortCombination.Host, hostPortCombination.Port);
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
            _context.ModbusConfigurations.Add(dbData);
        }
        else
        {
            _context.ModbusConfigurations.Update(dbData);
        }
        await _context.SaveChangesAsync().ConfigureAwait(false);
        await _refreshableValueHandlingService.RecreateRefreshables().ConfigureAwait(false);
        return dbData.Id;
    }

    public async Task<List<DelegateRefreshableValue<decimal>>> GetDecimalRefreshableValuesAsync(TimeSpan defaultInterval)
    {
        _logger.LogTrace("{method}({defaultInterval})", nameof(GetDecimalRefreshableValuesAsync), defaultInterval);
        var modbusConfigurations = await GetModbusConfigurationByPredicate(
                c => true).ConfigureAwait(false);

        var result = new List<DelegateRefreshableValue<decimal>>();
        foreach (var modbusConfiguration in modbusConfigurations)
        {
            try
            {
                var configuration = modbusConfiguration;
                var refreshable = new DelegateRefreshableValue<decimal>(
                    _serviceScopeFactory,
                    async ct =>
                    {
                        using var executionScope = _serviceScopeFactory.CreateScope();
                        var modbusValueConfigurationService = executionScope.ServiceProvider
                            .GetRequiredService<IModbusValueConfigurationService>();
                        var modbusValueExecutionService = executionScope.ServiceProvider
                            .GetRequiredService<IModbusValueExecutionService>();

                        var resultConfigurations = await modbusValueConfigurationService
                            .GetResultConfigurationsByValueConfigurationId(configuration.Id)
                            .ConfigureAwait(false);

                        var values = new Dictionary<ValueKey, decimal>();
                        foreach (var resultConfiguration in resultConfigurations)
                        {
                            ct.ThrowIfCancellationRequested();
                            var valueKey = new ValueKey(resultConfiguration.UsedFor, null, resultConfiguration.Id);
                            try
                            {
                                var byteArray = await modbusValueExecutionService
                                    .GetResult(configuration, resultConfiguration, false)
                                    .ConfigureAwait(false);
                                var value = await modbusValueExecutionService
                                    .GetValue(byteArray, resultConfiguration)
                                    .ConfigureAwait(false);

                                values.TryAdd(valueKey, 0m);
                                values[valueKey] = +value;

                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                var logger = executionScope.ServiceProvider.GetRequiredService<ILogger<ModbusValueConfigurationService>>();
                                logger.LogError(
                                    ex,
                                    "Error while refreshing modbus value for configuration {configurationId} result {resultId}",
                                    configuration.Id,
                                    resultConfiguration.Id);
                                throw;
                            }
                        }

                        return new ConcurrentDictionary<ValueKey, decimal>(values);
                    },
                    defaultInterval,
                    _constants.SolarHistoricValueCapacity,
                    new SourceValueKey(configuration.Id, ConfigurationType.ModbusSolarValue)
                );

                result.Add(refreshable);
            }
            catch (Exception ex)
            {
                _logger.LogError(
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
