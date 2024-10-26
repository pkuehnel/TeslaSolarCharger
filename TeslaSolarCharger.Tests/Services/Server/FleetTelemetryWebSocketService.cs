using System.Collections.Generic;
using System;
using TeslaSolarCharger.Shared.Dtos.ChargingCost.CostConfigurations;
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
}
