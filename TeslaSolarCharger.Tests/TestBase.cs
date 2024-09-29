using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Autofac;
using Autofac.Extras.FakeItEasy;
using Autofac.Extras.Moq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Linq;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.EntityFramework;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Resources.PossibleIssues;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.Shared.TimeProviding;
using TeslaSolarCharger.SharedBackend.MappingExtensions;
using TeslaSolarCharger.Tests.Data;
using Xunit.Abstractions;
using Constants = TeslaSolarCharger.Shared.Resources.Constants;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Services;
using TeslaSolarCharger.Services.Services;

namespace TeslaSolarCharger.Tests;

public class TestBase : IDisposable
{
    private static readonly ConcurrentDictionary<ITestOutputHelper, (ILoggerFactory, LoggingLevelSwitch)> LoggerFactoryCache = new();

    protected readonly AutoMock Mock;

    private readonly TeslaSolarChargerContext _ctx;
    protected readonly AutoFake _fake;

    protected ITeslaSolarChargerContext Context => _ctx;

    protected LoggingLevelSwitch LogLevelSwitch { get; }

    protected TestBase(
        ITestOutputHelper outputHelper,
        LogEventLevel setupLogEventLevel = LogEventLevel.Warning,
        // ReSharper disable once UnusedParameter.Local
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
                .AddInMemoryCollection(configDictionary!)
                .Build()
            ;

        var currentFakeTime = new DateTime(2023, 2, 2, 8, 0, 0);

        _fake = new AutoFake();
        _fake.Provide<IMapperConfigurationFactory, MapperConfigurationFactory>();
        _fake.Provide<IIssueKeys, IssueKeys>();
        _fake.Provide<IPossibleIssues, PossibleIssues>();
        _fake.Provide<IResultValueCalculationService, ResultValueCalculationService>();
        _fake.Provide<IConstants, Constants>();
        _fake.Provide<IDateTimeProvider>(new FakeDateTimeProvider(currentFakeTime));
        _fake.Provide<IConfiguration>(configuration);

        Mock = AutoMock.GetLoose(
            b =>
            {
                b.Register((_, _) => Context);
                b.Register((_, _) => _fake.Resolve<IMapperConfigurationFactory>());
                b.Register((_, _) => _fake.Resolve<IIssueKeys>());
                b.Register((_, _) => _fake.Resolve<IPossibleIssues>());
                b.Register((_, _) => _fake.Resolve<IResultValueCalculationService>());
                b.Register((_, _) => _fake.Resolve<IConstants>());
                b.Register((_, _) => _fake.Resolve<IConfiguration>());
                b.RegisterType<FakeDateTimeProvider>();
                //b.Register((_, _) => _fake.Resolve<IDateTimeProvider>());
            });

        // In-memory database only exists while the connection is open
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var (loggerFactory, logLevelSwitch) = GetOrCreateLoggerFactory(outputHelper, setupLogEventLevel);
        LogLevelSwitch = logLevelSwitch;

        _ = new DbContextOptionsBuilder<TeslaSolarChargerContext>()
            .UseLoggerFactory(loggerFactory)
            .UseSqlite(connection)
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .Options;


        var connection1 = new SqliteConnection("DataSource=:memory:");
        connection1.Open();

        using (var command = connection1.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = OFF;";
            command.ExecuteNonQuery();
        }

        var options = new DbContextOptionsBuilder<TeslaSolarChargerContext>()
            .UseLoggerFactory(loggerFactory)
            .UseSqlite(connection1)
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .Options;
        //var autoMock = AutoMock.GetLoose(cfg => cfg.RegisterInstance(new TeslaSolarChargerContext(options)).As<ITeslaSolarChargerContext>());
        //_ctx = (TeslaSolarChargerContext) autoMock.Create<ITeslaSolarChargerContext>();

        _ctx = _fake.Provide(new TeslaSolarChargerContext(options));
        _ctx.Database.EnsureCreated();
        _ctx.InitRestValueConfigurations();
        _ctx.InitLoggedErrors();
        _ctx.SaveChanges();
        DetachAllEntities();
    }

    protected void DetachAllEntities()
    {
        _ctx.ChangeTracker.Entries().Where(e => e.State != EntityState.Detached).ToList()
            .ForEach(entry => entry.State = EntityState.Detached);
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
