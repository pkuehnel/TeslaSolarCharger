using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Autofac.Extras.Moq;
using Moq;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Dtos.Settings;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingServiceV2;

public class AddNoOcppConnectionReasonTests : TestBase
{
    public AddNoOcppConnectionReasonTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Theory]
    [InlineData(true, true, false)]  // Managed, State Exists -> No Reason
    [InlineData(true, false, true)]  // Managed, State Missing -> Add Reason
    [InlineData(false, true, false)] // Not Managed, State Exists -> No Reason
    [InlineData(false, false, false)]// Not Managed, State Missing -> No Reason
    public async Task AddNoOcppConnectionReason_ScenarioTests(bool shouldBeManaged, bool stateExists, bool expectedCall)
    {
        // Arrange
        int connectorId = 123;

        // Seed the database with a connector
        var connector = new OcppChargingStationConnector("TestConnector")
        {
            Id = connectorId,
            ShouldBeManaged = shouldBeManaged
        };
        Context.OcppChargingStationConnectors.Add(connector);
        await Context.SaveChangesAsync();

        // Setup Settings Mock
        var states = new ConcurrentDictionary<int, DtoOcppConnectorState>();
        if (stateExists)
        {
            states.TryAdd(connectorId, new DtoOcppConnectorState());
        }
        Mock.Mock<ISettings>().Setup(s => s.OcppConnectorStates).Returns(states);

        // Setup Reason Helper Mock
        var reasonHelperMock = Mock.Mock<INotChargingWithExpectedPowerReasonHelper>();

        // Create the Service
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();

        // Act
        await service.AddNoOcppConnectionReason(CancellationToken.None);

        // Assert
        if (expectedCall)
        {
            reasonHelperMock.Verify(h => h.AddLoadPointSpecificReason(
                null,
                connectorId,
                It.Is<NotChargingWithExpectedPowerReasonTemplate>(r => r.LocalizationKey.Contains("OCPP connection not established"))),
                Times.Once);
        }
        else
        {
            reasonHelperMock.Verify(h => h.AddLoadPointSpecificReason(
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<NotChargingWithExpectedPowerReasonTemplate>()),
                Times.Never);
        }
    }

    [Fact]
    public async Task AddNoOcppConnectionReason_MultipleConnectors_AddsReasonOnlyForManagedMissingState()
    {
        // Arrange
        // Connector 1: Managed, Missing State -> Should Add Reason
        var c1 = new OcppChargingStationConnector("C1") { Id = 1, ShouldBeManaged = true };

        // Connector 2: Managed, Has State -> Should NOT Add Reason
        var c2 = new OcppChargingStationConnector("C2") { Id = 2, ShouldBeManaged = true };

        // Connector 3: Not Managed, Missing State -> Should NOT Add Reason
        var c3 = new OcppChargingStationConnector("C3") { Id = 3, ShouldBeManaged = false };

        Context.OcppChargingStationConnectors.AddRange(c1, c2, c3);
        await Context.SaveChangesAsync();

        // Setup Settings: Only C2 has state
        var states = new ConcurrentDictionary<int, DtoOcppConnectorState>();
        states.TryAdd(2, new DtoOcppConnectorState());

        Mock.Mock<ISettings>().Setup(s => s.OcppConnectorStates).Returns(states);
        var reasonHelperMock = Mock.Mock<INotChargingWithExpectedPowerReasonHelper>();

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();

        // Act
        await service.AddNoOcppConnectionReason(CancellationToken.None);

        // Assert
        // Verify C1: Managed & Missing State -> Called
        reasonHelperMock.Verify(h => h.AddLoadPointSpecificReason(
            null,
            1,
            It.Is<NotChargingWithExpectedPowerReasonTemplate>(r => r.LocalizationKey.Contains("OCPP connection not established"))),
            Times.Once);

        // Verify C2: Managed & Has State -> Not Called
        reasonHelperMock.Verify(h => h.AddLoadPointSpecificReason(null, 2, It.IsAny<NotChargingWithExpectedPowerReasonTemplate>()), Times.Never);

        // Verify C3: Not Managed -> Not Called
        reasonHelperMock.Verify(h => h.AddLoadPointSpecificReason(null, 3, It.IsAny<NotChargingWithExpectedPowerReasonTemplate>()), Times.Never);
    }
}
