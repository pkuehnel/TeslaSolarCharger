using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.ChargingStation;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Dtos.Setup;
using TeslaSolarCharger.SharedModel.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server.Setup;

/// <summary>
/// The assistant promises that nothing is switched on until the user finishes. A charging station announces itself
/// over OCPP rather than being added by hand, so it is the one piece of equipment that can arrive halfway through
/// setup - and it used to start being managed the moment it did.
/// </summary>
public class ChargingStationActivationBoundaryTests : TestBase
{
    private readonly Mock<IConfigurationWrapper> _configurationWrapper = new();
    private readonly Mock<ISetupStateService> _setupStateService = new();
    private readonly Mock<ISettings> _settings = new();
    private readonly ConcurrentDictionary<int, DtoOcppConnectorState> _connectorStates = new();

    /// <summary>The assistant's stored answers, non-null exactly while a setup session is open.</summary>
    private DtoSetupState? _openSetupState;

    public ChargingStationActivationBoundaryTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
        _settings.Setup(s => s.OcppConnectorStates).Returns(_connectorStates);
        _setupStateService.Setup(s => s.GetSetupState()).ReturnsAsync(() => _openSetupState);
    }

    private OcppChargingStationConfigurationService NewService() => new(
        Moq.Mock.Of<ILogger<OcppChargingStationConfigurationService>>(),
        Context,
        Moq.Mock.Of<IOcppChargePointConfigurationService>(),
        _configurationWrapper.Object,
        _setupStateService.Object,
        _settings.Object);

    private async Task<int> AddConnectedConnector()
    {
        Context.OcppChargingStations.Add(new OcppChargingStation("CP1") { Id = 1, });
        Context.OcppChargingStationConnectors.Add(new OcppChargingStationConnector("Connector 1")
        {
            Id = 5, OcppChargingStationId = 1, ConnectorId = 1, ShouldBeManaged = false,
        });
        await Context.SaveChangesAsync();
        DetachAllEntities();
        //A charger that has reported in and is talking to us right now.
        _connectorStates[5] = new DtoOcppConnectorState();
        return 5;
    }

    private static DtoChargingStationConnector ConnectorDto(int id) => new("Connector 1")
    {
        Id = id,
        ShouldBeManaged = false,
        MinCurrent = 6,
        MaxCurrent = 16,
        ConnectedPhasesCount = 3,
        AllowedCars = new HashSet<int>(),
    };

    [Fact]
    public async Task AChargerThatConnectsDuringSetupStaysSwitchedOff()
    {
        var connectorId = await AddConnectedConnector();
        _configurationWrapper.Setup(w => w.IsFirstRun()).Returns(true);
        _openSetupState = new DtoSetupState();

        await NewService().UpdateChargingStationConnector(ConnectorDto(connectorId));

        var connector = await Context.OcppChargingStationConnectors.FirstAsync(c => c.Id == connectorId);
        //Otherwise the "enable when setup finishes" choice and Save without enabling describe something other than
        //what is actually running.
        Assert.False(connector.ShouldBeManaged);
    }

    [Fact]
    public async Task AChargerThatConnectsToAConfiguredInstallationStartsWorking()
    {
        var connectorId = await AddConnectedConnector();
        _configurationWrapper.Setup(w => w.IsFirstRun()).Returns(false);
        _openSetupState = null;

        await NewService().UpdateChargingStationConnector(ConnectorDto(connectorId));

        var connector = await Context.OcppChargingStationConnectors.FirstAsync(c => c.Id == connectorId);
        //Nobody is in the assistant: plugging a charger in and having it work is exactly what the user expects.
        Assert.True(connector.ShouldBeManaged);
    }

    [Fact]
    public async Task AChargerThatConnectsWhileSetupIsReopenedStaysSwitchedOff()
    {
        //Reopening the assistant on a working installation to add a second charger makes exactly the same promise
        //as a first run does. Guarding only on the first run kept it for new users and broke it for everyone else.
        var connectorId = await AddConnectedConnector();
        _configurationWrapper.Setup(w => w.IsFirstRun()).Returns(false);
        _openSetupState = new DtoSetupState();

        await NewService().UpdateChargingStationConnector(ConnectorDto(connectorId));

        var connector = await Context.OcppChargingStationConnectors.FirstAsync(c => c.Id == connectorId);
        Assert.False(connector.ShouldBeManaged);
    }

    [Fact]
    public async Task AnUnreadableSetupStateLeavesTheChargerSwitchedOff()
    {
        var connectorId = await AddConnectedConnector();
        _configurationWrapper.Setup(w => w.IsFirstRun()).Returns(false);
        _setupStateService.Setup(s => s.GetSetupState()).ThrowsAsync(new InvalidOperationException("storage is gone"));

        await NewService().UpdateChargingStationConnector(ConnectorDto(connectorId));

        var connector = await Context.OcppChargingStationConnectors.FirstAsync(c => c.Id == connectorId);
        //Not knowing whether setup is open is not a reason to switch something on behind the user's back.
        Assert.False(connector.ShouldBeManaged);
    }

    [Fact]
    public async Task AnExplicitlyEnabledConnectorIsManagedEvenDuringSetup()
    {
        var connectorId = await AddConnectedConnector();
        _configurationWrapper.Setup(w => w.IsFirstRun()).Returns(true);
        _openSetupState = new DtoSetupState();
        var dto = ConnectorDto(connectorId);
        dto.ShouldBeManaged = true;

        await NewService().UpdateChargingStationConnector(dto);

        var connector = await Context.OcppChargingStationConnectors.FirstAsync(c => c.Id == connectorId);
        //Activation asked for by name is still activation; only the automatic kind is held back.
        Assert.True(connector.ShouldBeManaged);
    }
}
