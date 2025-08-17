using System;
using TeslaSolarCharger.Server.Helper;
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
            Type = CarValueType.StateOfCharge,
            IntValue = 10,
        };
        var fleetTelemetryWebSocketService = Mock.Create<CarPropertyUpdateHelper>();
        var car = new DtoCar();
        fleetTelemetryWebSocketService.UpdateDtoCarProperty(car, carValueLog);
        Assert.Equal(10, car.SoC.Value);
        var olderValue = new TeslaSolarCharger.Model.Entities.TeslaSolarCharger.CarValueLog
        {
            Timestamp = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Type = CarValueType.StateOfCharge,
            IntValue = 12,
        };
        fleetTelemetryWebSocketService.UpdateDtoCarProperty(car, olderValue);
        Assert.Equal(10, car.SoC.Value);

        var newerValue = new TeslaSolarCharger.Model.Entities.TeslaSolarCharger.CarValueLog
        {
            Timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Type = CarValueType.StateOfCharge,
            IntValue = 12,
        };
        fleetTelemetryWebSocketService.UpdateDtoCarProperty(car, newerValue);
        Assert.Equal(12, car.SoC.Value);
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
            Timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IntValue = 10,
            Type = CarValueType.StateOfCharge,
        };
        var fleetTelemetryWebSocketService = Mock.Create<CarPropertyUpdateHelper>();
        var car = new DtoCar();
        fleetTelemetryWebSocketService.UpdateDtoCarProperty(car, carValueLog);
        Assert.Equal(10, car.SoC.Value);
    }

    [Fact]
    public void CanSetIntValueFromDoubleValue()
    {
        var carValueLog = new TeslaSolarCharger.Model.Entities.TeslaSolarCharger.CarValueLog
        {
            Timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DoubleValue = 10.45848,
            Type = CarValueType.StateOfCharge,
        };
        var fleetTelemetryWebSocketService = Mock.Create<CarPropertyUpdateHelper>();
        var car = new DtoCar();
        fleetTelemetryWebSocketService.UpdateDtoCarProperty(car, carValueLog);
        Assert.Equal(10, car.SoC.Value);
    }

    [Fact]
    public void CanSetIntValueFromStringValue()
    {
        var carValueLog = new TeslaSolarCharger.Model.Entities.TeslaSolarCharger.CarValueLog
        {
            Timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            StringValue = "10.45848",
            Type = CarValueType.StateOfCharge,
        };
        var fleetTelemetryWebSocketService = Mock.Create<CarPropertyUpdateHelper>();
        var car = new DtoCar();
        fleetTelemetryWebSocketService.UpdateDtoCarProperty(car, carValueLog);
        Assert.Equal(10, car.SoC.Value);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    public void CanSetBoolValueFromTrueStringValue(string boolValue)
    {
        var carValueLog = new TeslaSolarCharger.Model.Entities.TeslaSolarCharger.CarValueLog
        {
            Timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            StringValue = boolValue,
            Type = CarValueType.IsPluggedIn,
        };
        var fleetTelemetryWebSocketService = Mock.Create<CarPropertyUpdateHelper>();
        var car = new DtoCar();
        fleetTelemetryWebSocketService.UpdateDtoCarProperty(car, carValueLog);
        Assert.True(car.PluggedIn.Value);
    }

    [Fact]
    public void CanSetDoubleValueFromDoubleValue()
    {
        var carValueLog = new TeslaSolarCharger.Model.Entities.TeslaSolarCharger.CarValueLog
        {
            Timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DoubleValue = 10.45848,
            Type = CarValueType.Latitude,
        };
        var fleetTelemetryWebSocketService = Mock.Create<CarPropertyUpdateHelper>();
        var car = new DtoCar();
        fleetTelemetryWebSocketService.UpdateDtoCarProperty(car, carValueLog);
        Assert.Equal(10.45848, car.Latitude.Value);
    }

    [Fact]
    public void CanSetDoubleValueFromIntValue()
    {
        var carValueLog = new TeslaSolarCharger.Model.Entities.TeslaSolarCharger.CarValueLog
        {
            Timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IntValue = 10,
            Type = CarValueType.Latitude,
        };
        var fleetTelemetryWebSocketService = Mock.Create<CarPropertyUpdateHelper>();
        var car = new DtoCar();
        fleetTelemetryWebSocketService.UpdateDtoCarProperty(car, carValueLog);
    }

    [Fact]
    public void CanSetDoubleValueFromStringValue()
    {
        var carValueLog = new TeslaSolarCharger.Model.Entities.TeslaSolarCharger.CarValueLog
        {
            Timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            StringValue = "10.45848",
            Type = CarValueType.Latitude,
        };
        var fleetTelemetryWebSocketService = Mock.Create<CarPropertyUpdateHelper>();
        var car = new DtoCar();
        fleetTelemetryWebSocketService.UpdateDtoCarProperty(car, carValueLog);
        Assert.Equal(10.45848, car.Latitude.Value);
    }
}
