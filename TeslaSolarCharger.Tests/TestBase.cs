using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Autofac;
using Autofac.Extras.Moq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.EntityFramework;
using TeslaSolarCharger.Shared.TimeProviding;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests;

public class TestBase : IDisposable
{
    private static readonly ConcurrentDictionary<ITestOutputHelper, (ILoggerFactory, LoggingLevelSwitch)> LoggerFactoryCache = new();

    protected readonly AutoMock Mock;

    protected readonly ITeslaSolarChargerContext _ctx;

    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;

    protected LoggingLevelSwitch LogLevelSwitch { get; }

    protected TestBase(
        ITestOutputHelper outputHelper,
        LogEventLevel setupLogEventLevel = LogEventLevel.Warning,
        LogEventLevel defaultLogEventLevel = LogEventLevel.Debug)
    {

        var configDictionary = new Dictionary<string, string>
        {
            {"TeslaMateApiBaseUrl", "http://192.168.1.50:8097"},
            {"ten", "10"},
            {"one", "1"},
            {"zero", "0"},
            {"ConfigFileLocation", "configs"},
            {"CarConfigFilename", "carConfig.json"},
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDictionary)
            .Build()
            ;
        
        Mock = AutoMock.GetLoose(cfg =>
        {
            cfg.RegisterType(typeof(FakeDateTimeProvider));
            cfg.RegisterInstance(configuration).As<IConfiguration>();
        });

        // In-memory database only exists while the connection is open
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var (loggerFactory, logLevelSwitch) = GetOrCreateLoggerFactory(outputHelper, setupLogEventLevel);
        LogLevelSwitch = logLevelSwitch;

        var options = new DbContextOptionsBuilder<TeslaSolarChargerContext>()
            .UseLoggerFactory(loggerFactory)
            .UseSqlite(_connection)
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .Options;

        //ToDo: Create demo database for tests
        //_ctx = (TeslaSolarChargerContext)Mock
        //    .Provide<ITeslaSolarChargerContext>(new TeslaSolarChargerContext(options));
        //_ctx.Database.EnsureCreated();
    }

    private static (ILoggerFactory, LoggingLevelSwitch) GetOrCreateLoggerFactory(
        ITestOutputHelper testOutputHelper,
        LogEventLevel logEventLevel)
    {
        if (testOutputHelper is null)
        {
            throw new ArgumentNullException(nameof(testOutputHelper));
        }

        var tuple = LoggerFactoryCache.GetOrAdd(testOutputHelper, CreateLoggerFactory(testOutputHelper));
        tuple.Item2.MinimumLevel = logEventLevel;

        return tuple;
    }

    private static (ILoggerFactory, LoggingLevelSwitch) CreateLoggerFactory(ITestOutputHelper testOutputHelper)
    {
        var loggingLevelSwitch = new LoggingLevelSwitch(LogEventLevel.Verbose);

        var factory = LoggerFactory.Create(builder =>
        {
            var serilog = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(loggingLevelSwitch)
                .WriteTo.TestOutput(testOutputHelper,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            builder.SetMinimumLevel(LogLevel.Trace)
                .AddDebug()
                .AddSerilog(serilog, true);
        });

        return (factory, loggingLevelSwitch);
    }

    public void Dispose()
    {
        Mock.Dispose();
    }
}
