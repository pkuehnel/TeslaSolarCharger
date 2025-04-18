﻿using Microsoft.EntityFrameworkCore;
using PkSoftwareService.Custom.Backend;
using Serilog;
using Serilog.Events;
using System.Text;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Support;

namespace TeslaSolarCharger.Server.Services;

public class DebugService(ILogger<DebugService> logger,
    ITeslaSolarChargerContext context,
    IInMemorySink inMemorySink,
    Serilog.Core.LoggingLevelSwitch inMemoryLogLevelSwitch) : IDebugService
{
    public async Task<Dictionary<int, DtoDebugCar>> GetCars()
    {
        logger.LogTrace("{method}", nameof(GetCars));
        var cars = await context.Cars
            .Where(x => x.Vin != null)
            .ToDictionaryAsync(x => x.Id, x => new DtoDebugCar()
            {
                Name = x.Name,
                Vin = x.Vin,
                ShouldBeManaged = x.ShouldBeManaged == true,
                IsAvailableInTeslaAccount = x.IsAvailableInTeslaAccount,
            }).ConfigureAwait(false);
        logger.LogDebug("Found {carCount} cars", cars.Count);
        return cars;
    }

    public byte[] GetLogBytes()
    {
        logger.LogTrace("{method}", nameof(GetLogBytes));
        var logEntries = inMemorySink.GetLogs();
        var content = string.Join(Environment.NewLine, logEntries);
        var bytes = Encoding.UTF8.GetBytes(content);
        return bytes;
    }

    public string GetLogLevel()
    {
        logger.LogTrace("{method}", nameof(GetLogLevel));
        return inMemoryLogLevelSwitch.MinimumLevel.ToString();
    }

    public void SetLogLevel(string level)
    {
        logger.LogTrace("{method} {level}", nameof(SetLogLevel), level);
        if (!Enum.TryParse<LogEventLevel>(level, true, out var newLevel))
        {
            throw new ArgumentException("Invalid log level. Use one of: Verbose, Debug, Information, Warning, Error, Fatal", nameof(level));
        }
        inMemoryLogLevelSwitch.MinimumLevel = newLevel;
    }

    public int GetLogCapacity()
    {
        logger.LogTrace("{method}", nameof(GetLogCapacity));
        return inMemorySink.GetCapacity();
    }

    public void SetLogCapacity(int capacity)
    {
        logger.LogTrace("{method} {capacity}", nameof(SetLogCapacity), capacity);
        inMemorySink.UpdateCapacity(capacity);
    }
}
