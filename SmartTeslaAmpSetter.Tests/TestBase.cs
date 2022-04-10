using System;
using System.Collections.Concurrent;
using Autofac.Extras.Moq;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit.Abstractions;

namespace SmartTeslaAmpSetter.Tests;

public class TestBase : IDisposable
{
    private static readonly ConcurrentDictionary<ITestOutputHelper, (ILoggerFactory, LoggingLevelSwitch)> LoggerFactoryCache = new ConcurrentDictionary<ITestOutputHelper, (ILoggerFactory, LoggingLevelSwitch)>();

    protected readonly AutoMock Mock;

    protected LoggingLevelSwitch LogLevelSwitch { get; }

    protected TestBase(
        ITestOutputHelper outputHelper,
        LogEventLevel setupLogEventLevel = LogEventLevel.Warning,
        LogEventLevel defaultLogEventLevel = LogEventLevel.Debug)
    {
        Mock = AutoMock.GetLoose();

        // ReSharper disable once UnusedVariable
        var (loggerFactory, logLevelSwitch) = GetOrCreateLoggerFactory(outputHelper, setupLogEventLevel);
        LogLevelSwitch = logLevelSwitch;

        LogLevelSwitch.MinimumLevel = defaultLogEventLevel;
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