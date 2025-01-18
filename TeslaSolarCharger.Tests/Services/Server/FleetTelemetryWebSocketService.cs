using System.Collections.Generic;
using System;
using TeslaSolarCharger.Shared.Dtos.ChargingCost.CostConfigurations;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using Xunit.Abstractions;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class FleetTelemetryWebSocketService : TestBase
{
    public FleetTelemetryWebSocketService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public void OnlySetsNewerValues()
    {
        var carValueLog = new TeslaSolarCharger.Model.Entities.TeslaSolarCharger.CarValueLog
        {
            Timestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IntValue = 10,
        };
        var fleetTelemetryWebSocketService = Mock.Create<TeslaSolarCharger.Server.Services.FleetTelemetryWebSocketService>();
        var car = new DtoCar();
        fleetTelemetryWebSocketService.UpdateDtoCarProperty(car, carValueLog, nameof(DtoCar.SoC));
        Assert.Equal(10, car.SoC);
        var olderValue = new TeslaSolarCharger.Model.Entities.TeslaSolarCharger.CarValueLog
        {
            Timestamp = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IntValue = 12,
        };
        fleetTelemetryWebSocketService.UpdateDtoCarProperty(car, olderValue, nameof(DtoCar.SoC));
        Assert.Equal(10, car.SoC);

        var newerValue = new TeslaSolarCharger.Model.Entities.TeslaSolarCharger.CarValueLog
        {
            Timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IntValue = 12,
        };
        fleetTelemetryWebSocketService.UpdateDtoCarProperty(car, newerValue, nameof(DtoCar.SoC));
        Assert.Equal(12, car.SoC);
    }

    [Fact]
    public void CanDeserializeKnownEnumValues()
    {
        var message = "{\"Type\":1,\"DoubleValue\":15.0,\"IntValue\":null,\"StringValue\":null,\"UnknownValue\":null,\"TimeStamp\":\"2024-10-26T09:56:24.9895636+00:00\"}";
        var fleetTelemetryWebSocketService = Mock.Create<TeslaSolarCharger.Server.Services.FleetTelemetryWebSocketService>();
        var result = fleetTelemetryWebSocketService.DeserializeFleetTelemetryMessage(message);
        Assert.NotNull(result);
        Assert.Equal(CarValueType.ModuleTempMax, result.Type);
    }

    [Fact]
    public void CanDeserializeUnKnownEnumValues()
    {
        var message = "{\"Type\":364,\"DoubleValue\":15.0,\"IntValue\":null,\"StringValue\":null,\"UnknownValue\":null,\"TimeStamp\":\"2024-10-26T09:56:24.9895636+00:00\"}";
        var fleetTelemetryWebSocketService = Mock.Create<TeslaSolarCharger.Server.Services.FleetTelemetryWebSocketService>();
        var result = fleetTelemetryWebSocketService.DeserializeFleetTelemetryMessage(message);
        Assert.NotNull(result);
        Assert.Equal(CarValueType.Unknown, result.Type);
    }

    [Fact]
    public void CanSetIntValueFromIntValue()
    {
        var carValueLog = new TeslaSolarCharger.Model.Entities.TeslaSolarCharger.CarValueLog
        {
            IntValue = 10,
        };
        var fleetTelemetryWebSocketService = Mock.Create<TeslaSolarCharger.Server.Services.FleetTelemetryWebSocketService>();
        var car = new DtoCar();
        fleetTelemetryWebSocketService.UpdateDtoCarProperty(car, carValueLog, nameof(DtoCar.SoC));
        Assert.Equal(10, car.SoC);
    }

    [Fact]
    public void CanSetIntValueFromDoubleValue()
    {
        var carValueLog = new TeslaSolarCharger.Model.Entities.TeslaSolarCharger.CarValueLog
        {
            DoubleValue = 10.45848,
        };
        var fleetTelemetryWebSocketService = Mock.Create<TeslaSolarCharger.Server.Services.FleetTelemetryWebSocketService>();
        var car = new DtoCar();
        fleetTelemetryWebSocketService.UpdateDtoCarProperty(car, carValueLog, nameof(DtoCar.SoC));
        Assert.Equal(10, car.SoC);
    }

    [Fact]
    public void CanSetIntValueFromStringValue()
    {
        var carValueLog = new TeslaSolarCharger.Model.Entities.TeslaSolarCharger.CarValueLog
        {
            StringValue = "10.45848",
        };
        var fleetTelemetryWebSocketService = Mock.Create<TeslaSolarCharger.Server.Services.FleetTelemetryWebSocketService>();
        var car = new DtoCar();
        fleetTelemetryWebSocketService.UpdateDtoCarProperty(car, carValueLog, nameof(DtoCar.SoC));
        Assert.Equal(10, car.SoC);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    public void CanSetBoolValueFromTrueStringValue(string boolValue)
    {
        var carValueLog = new TeslaSolarCharger.Model.Entities.TeslaSolarCharger.CarValueLog
        {
            StringValue = boolValue,
        };
        var fleetTelemetryWebSocketService = Mock.Create<TeslaSolarCharger.Server.Services.FleetTelemetryWebSocketService>();
        var car = new DtoCar();
        fleetTelemetryWebSocketService.UpdateDtoCarProperty(car, carValueLog, nameof(DtoCar.PluggedIn));
        Assert.True(car.PluggedIn);
    }

    [Fact]
    public void CanSetDoubleValueFromDoubleValue()
    {
        var carValueLog = new TeslaSolarCharger.Model.Entities.TeslaSolarCharger.CarValueLog
        {
            DoubleValue = 10.45848,
        };
        var fleetTelemetryWebSocketService = Mock.Create<TeslaSolarCharger.Server.Services.FleetTelemetryWebSocketService>();
        var car = new DtoCar();
        fleetTelemetryWebSocketService.UpdateDtoCarProperty(car, carValueLog, nameof(DtoCar.Latitude));
        Assert.Equal(10.45848, car.Latitude);
    }

    [Fact]
    public void CanSetDoubleValueFromIntValue()
    {
        var carValueLog = new TeslaSolarCharger.Model.Entities.TeslaSolarCharger.CarValueLog
        {
            IntValue = 10,
        };
        var fleetTelemetryWebSocketService = Mock.Create<TeslaSolarCharger.Server.Services.FleetTelemetryWebSocketService>();
        var car = new DtoCar();
        fleetTelemetryWebSocketService.UpdateDtoCarProperty(car, carValueLog, nameof(DtoCar.Latitude));
        Assert.Equal(10, car.Latitude);
    }

    [Fact]
    public void CanSetDoubleValueFromStringValue()
    {
        var carValueLog = new TeslaSolarCharger.Model.Entities.TeslaSolarCharger.CarValueLog
        {
            StringValue = "10.45848",
        };
        var fleetTelemetryWebSocketService = Mock.Create<TeslaSolarCharger.Server.Services.FleetTelemetryWebSocketService>();
        var car = new DtoCar();
        fleetTelemetryWebSocketService.UpdateDtoCarProperty(car, carValueLog, nameof(DtoCar.Latitude));
        Assert.Equal(10.45848, car.Latitude);
    }
}
