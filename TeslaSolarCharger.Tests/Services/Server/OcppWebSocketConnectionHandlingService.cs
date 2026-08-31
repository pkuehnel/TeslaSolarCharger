using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using Xunit;


namespace TeslaSolarCharger.Tests.Services.Server;

public class OcppWebSocketConnectionHandlingService : TestBase
{
    public OcppWebSocketConnectionHandlingService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Theory]
    [InlineData("[2,\"2025051820053400000095\",\"MeterValues\",{\"connectorId\":1,\"transactionId\":10,\"meterValue\":[{\"timestamp\":\"2025-05-18T20:07:49.000Z\",\"sampledValue\":[{\"value\":\"1402812\",\"unit\":\"Wh\",\"measurand\":\"Energy.Active.Import.Register\"},{\"value\":\"0.000000\",\"unit\":\"V\",\"measurand\":\"Voltage\",\"phase\":\"L1\"},{\"value\":\"0.000000\",\"unit\":\"V\",\"measurand\":\"Voltage\",\"phase\":\"L2\"},{\"value\":\"0.000000\",\"unit\":\"V\",\"measurand\":\"Voltage\",\"phase\":\"L3\"},{\"value\":\"0.009000\",\"unit\":\"A\",\"measurand\":\"Current.Export\",\"phase\":\"L1\"},{\"value\":\"0.009000\",\"unit\":\"A\",\"measurand\":\"Current.Import\",\"phase\":\"L1\"},{\"value\":\"0.008000\",\"unit\":\"A\",\"measurand\":\"Current.Export\",\"phase\":\"L2\"},{\"value\":\"0.008000\",\"unit\":\"A\",\"measurand\":\"Current.Import\",\"phase\":\"L2\"},{\"value\":\"0.009000\",\"unit\":\"A\",\"measurand\":\"Current.Export\",\"phase\":\"L3\"},{\"value\":\"0.009000\",\"unit\":\"A\",\"measurand\":\"Current.Import\",\"phase\":\"L3\"},{\"value\":\"0\",\"unit\":\"W\",\"measurand\":\"Power.Offered\"},{\"value\":\"0\",\"unit\":\"W\",\"measurand\":\"Power.Active.Import\"}]}]}]")]
    [InlineData("[2,\"2025051820053900000096\",\"MeterValues\",{\"connectorId\":1,\"transactionId\":10,\"meterValue\":[{\"timestamp\":\"2025-05-18T20:07:51.000Z\",\"sampledValue\":[{\"value\":\"1402812\",\"unit\":\"Wh\",\"measurand\":\"Energy.Active.Import.Register\"},{\"value\":\"0.000000\",\"unit\":\"V\",\"measurand\":\"Voltage\",\"phase\":\"L1\"},{\"value\":\"0.000000\",\"unit\":\"V\",\"measurand\":\"Voltage\",\"phase\":\"L2\"},{\"value\":\"0.000000\",\"unit\":\"V\",\"measurand\":\"Voltage\",\"phase\":\"L3\"},{\"value\":\"0.008000\",\"unit\":\"A\",\"measurand\":\"Current.Export\",\"phase\":\"L1\"},{\"value\":\"0.008000\",\"unit\":\"A\",\"measurand\":\"Current.Import\",\"phase\":\"L1\"},{\"value\":\"0.009000\",\"unit\":\"A\",\"measurand\":\"Current.Export\",\"phase\":\"L2\"},{\"value\":\"0.009000\",\"unit\":\"A\",\"measurand\":\"Current.Import\",\"phase\":\"L2\"},{\"value\":\"0.008000\",\"unit\":\"A\",\"measurand\":\"Current.Export\",\"phase\":\"L3\"},{\"value\":\"0.008000\",\"unit\":\"A\",\"measurand\":\"Current.Import\",\"phase\":\"L3\"},{\"value\":\"0\",\"unit\":\"W\",\"measurand\":\"Power.Offered\"},{\"value\":\"0\",\"unit\":\"W\",\"measurand\":\"Power.Active.Import\"}]}]}]")]
    [InlineData("[2,\"202505182007530000009C\",\"MeterValues\",{\"connectorId\":1,\"transactionId\":10,\"meterValue\":[{\"timestamp\":\"2025-05-18T20:05:11.000Z\",\"sampledValue\":[{\"value\":\"1402664\",\"context\":\"Transaction.Begin\",\"format\":\"Raw\",\"measurand\":\"Energy.Active.Import.Register\",\"phase\":\"L3\",\"location\":\"Outlet\",\"unit\":\"Wh\"}]}]}]")]
    [InlineData("[2,\"2025051820072700000099\",\"MeterValues\",{\"connectorId\":1,\"transactionId\":10,\"meterValue\":[{\"timestamp\":\"2025-05-18T20:07:53.000Z\",\"sampledValue\":[{\"value\":\"1402812\",\"unit\":\"Wh\",\"measurand\":\"Energy.Active.Import.Register\"},{\"value\":\"0.000000\",\"unit\":\"V\",\"measurand\":\"Voltage\",\"phase\":\"L1\"},{\"value\":\"0.000000\",\"unit\":\"V\",\"measurand\":\"Voltage\",\"phase\":\"L2\"},{\"value\":\"0.000000\",\"unit\":\"V\",\"measurand\":\"Voltage\",\"phase\":\"L3\"},{\"value\":\"0.009000\",\"unit\":\"A\",\"measurand\":\"Current.Export\",\"phase\":\"L1\"},{\"value\":\"0.009000\",\"unit\":\"A\",\"measurand\":\"Current.Import\",\"phase\":\"L1\"},{\"value\":\"0.009000\",\"unit\":\"A\",\"measurand\":\"Current.Export\",\"phase\":\"L2\"},{\"value\":\"0.009000\",\"unit\":\"A\",\"measurand\":\"Current.Import\",\"phase\":\"L2\"},{\"value\":\"0.009000\",\"unit\":\"A\",\"measurand\":\"Current.Export\",\"phase\":\"L3\"},{\"value\":\"0.009000\",\"unit\":\"A\",\"measurand\":\"Current.Import\",\"phase\":\"L3\"},{\"value\":\"0\",\"unit\":\"W\",\"measurand\":\"Power.Offered\"},{\"value\":\"0\",\"unit\":\"W\",\"measurand\":\"Power.Active.Import\"}]}]}]")]
    [InlineData("[2,\"202505182007550000009D\",\"MeterValues\",{\"connectorId\":1,\"transactionId\":10,\"meterValue\":[{\"timestamp\":\"2025-05-18T20:07:34.000Z\",\"sampledValue\":[{\"value\":\"1402812\",\"context\":\"Transaction.End\",\"format\":\"Raw\",\"measurand\":\"Energy.Active.Import.Register\",\"phase\":\"L3\",\"location\":\"Outlet\",\"unit\":\"Wh\"}]}]}]")]
    [InlineData("[2,\"202505182007330000009A\",\"MeterValues\",{\"connectorId\":1,\"transactionId\":10,\"meterValue\":[{\"timestamp\":\"2025-05-18T20:07:55.000Z\",\"sampledValue\":[{\"value\":\"1402812\",\"unit\":\"Wh\",\"measurand\":\"Energy.Active.Import.Register\"},{\"value\":\"0.000000\",\"unit\":\"V\",\"measurand\":\"Voltage\",\"phase\":\"L1\"},{\"value\":\"0.000000\",\"unit\":\"V\",\"measurand\":\"Voltage\",\"phase\":\"L2\"},{\"value\":\"0.000000\",\"unit\":\"V\",\"measurand\":\"Voltage\",\"phase\":\"L3\"},{\"value\":\"0.009000\",\"unit\":\"A\",\"measurand\":\"Current.Export\",\"phase\":\"L1\"},{\"value\":\"0.009000\",\"unit\":\"A\",\"measurand\":\"Current.Import\",\"phase\":\"L1\"},{\"value\":\"0.009000\",\"unit\":\"A\",\"measurand\":\"Current.Export\",\"phase\":\"L2\"},{\"value\":\"0.009000\",\"unit\":\"A\",\"measurand\":\"Current.Import\",\"phase\":\"L2\"},{\"value\":\"0.008000\",\"unit\":\"A\",\"measurand\":\"Current.Export\",\"phase\":\"L3\"},{\"value\":\"0.008000\",\"unit\":\"A\",\"measurand\":\"Current.Import\",\"phase\":\"L3\"},{\"value\":\"0\",\"unit\":\"W\",\"measurand\":\"Power.Offered\"},{\"value\":\"0\",\"unit\":\"W\",\"measurand\":\"Power.Active.Import\"}]}]}]")]
    public void CanDeserializeMeterValues(string json)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var payload = root.GetArrayLength() >= 4 ? root[3] : default;
        var service = Mock.Create<TeslaSolarCharger.Server.Services.OcppWebSocketConnectionHandlingService>();
        var response = service.HandleMeterValues("asdf", "asdf", payload);
    }

    /// <summary>
    /// A charge point drains its offline queue after a reconnect, interleaved with live samples. One measured in the
    /// field was about seven minutes behind, so the published power alternated between now and minutes ago every few
    /// seconds while a charge was being stopped.
    ///
    /// The per value timestamp guard cannot catch that on its own: a reconnect installs a fresh connector state whose
    /// timestamps start at their minimum, so the first replayed sample is newer than nothing and wins. Both cases are
    /// covered here on exactly that fresh state.
    /// </summary>
    [Theory]
    //Half a minute old: a normal live sample.
    [InlineData(0.5, 4200, 6.0)]
    //Seven minutes old, which is what the replayed queue looked like.
    [InlineData(7, 0, 0.0)]
    public async Task OnlyMeterValuesThatDescribeThePresentBecomeTheCurrentState(
        double sampleAgeMinutes, int expectedPower, double expectedCurrent)
    {
        const string chargePointId = "test";
        var now = CurrentFakeDate;
        Mock.Mock<IDateTimeProvider>().Setup(p => p.DateTimeOffSetUtcNow()).Returns(now);
        //Built from the same instant the service is told it is, so the test states an age rather than a date.
        var sampleTimestamp = now.AddMinutes(-sampleAgeMinutes).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var station = new OcppChargingStation(chargePointId);
        var connector = new OcppChargingStationConnector("connector") { ConnectorId = 1, OcppChargingStation = station, };
        Context.OcppChargingStationConnectors.Add(connector);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        //Exactly what a reconnect leaves behind: everything at its default with a minimum timestamp.
        var connectorState = new DtoOcppConnectorState();
        var states = new ConcurrentDictionary<int, DtoOcppConnectorState>();
        states.TryAdd(connector.Id, connectorState);
        Mock.Mock<ISettings>().Setup(s => s.OcppConnectorStates).Returns(states);

        var scopedServiceProvider = new Mock<IServiceProvider>();
        scopedServiceProvider.Setup(p => p.GetService(typeof(ITeslaSolarChargerContext))).Returns(Context);
        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(scopedServiceProvider.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);
        Mock.Mock<IServiceProvider>().Setup(p => p.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactory.Object);

        var json = $$"""
                     [2,"id","MeterValues",{"connectorId":1,"transactionId":1,"meterValue":[{"timestamp":"{{sampleTimestamp}}",
                     "sampledValue":[
                     {"value":"700","unit":"W","measurand":"Power.Active.Import","phase":"L1"},
                     {"value":"700","unit":"W","measurand":"Power.Active.Import","phase":"L2"},
                     {"value":"2800","unit":"W","measurand":"Power.Active.Import","phase":"L3"},
                     {"value":"6.0","unit":"A","measurand":"Current.Import","phase":"L1"}]}]}]
                     """;
        var payload = JsonDocument.Parse(json).RootElement[3];
        var service = Mock.Create<TeslaSolarCharger.Server.Services.OcppWebSocketConnectionHandlingService>();

        //The charge point is answered either way: an unanswered Call is what makes it hang up.
        var response = await service.HandleMeterValues(chargePointId, "id", payload);
        Assert.Contains("\"id\"", response);

        Assert.Equal(expectedPower, connectorState.ChargingPower.Value);
        Assert.Equal((decimal)expectedCurrent, connectorState.ChargingCurrent.Value);
    }
}
