using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Ocpp;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.ChargepointAction;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class PilotCurrentProbeServiceTests : TestBase
{
    private const int ConnectorId = 1;
    private const int SecondConnectorId = 2;
    private const int CarA = 10;
    private const int CarB = 11;

    private readonly Settings _settings = new();
    private readonly DateTimeOffset _plugInTime;
    private readonly List<int> _startedCurrents = new();

    public PilotCurrentProbeServiceTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
        _plugInTime = CurrentFakeDate.AddMinutes(-5);
        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.Setup(s => s.Cars).Returns(_settings.Cars);
        settingsMock.Setup(s => s.OcppConnectorStates).Returns(_settings.OcppConnectorStates);
        settingsMock.Setup(s => s.ConfirmedLoadPointCarCombinations).Returns(_settings.ConfirmedLoadPointCarCombinations);
        settingsMock.Setup(s => s.LastPilotCurrentProbes).Returns(_settings.LastPilotCurrentProbes);
        Mock.Mock<IDateTimeProvider>().Setup(d => d.DateTimeOffSetUtcNow()).Returns(CurrentFakeDate);
        var configurationWrapper = Mock.Mock<IConfigurationWrapper>();
        configurationWrapper.Setup(c => c.GetVehicleDataFromTesla()).Returns(true);
        configurationWrapper.Setup(c => c.GetVehicleDataViaBle()).Returns(true);
        //What the configuration wrapper returns when no max combined current is set.
        configurationWrapper.Setup(c => c.MaxCombinedCurrent()).Returns(int.MaxValue);
        SetChargingCurrents();
        var actionService = Mock.Mock<IOcppChargePointActionService>();
        actionService.Setup(a => a.StartCharging(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback((int _, decimal current, int? _, CancellationToken _) => _startedCurrents.Add((int)current))
            .ReturnsAsync(new Result<RemoteStartTransactionResponse?>(new RemoteStartTransactionResponse(), null, null));
        actionService.Setup(a => a.StopCharging(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<RemoteStopTransactionResponse?>(new RemoteStopTransactionResponse(), null, null));
        actionService.Setup(a => a.SetChargingCurrent(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<SetChargingProfileResponse?>(new SetChargingProfileResponse(), null, null));
    }

    [Theory]
    [InlineData(6, 16, new int[] { }, 6)]
    [InlineData(6, 16, new[] { 16 }, 6)]
    [InlineData(6, 16, new[] { 6 }, 7)]
    [InlineData(8, 16, new[] { 8, 9 }, 10)]
    [InlineData(2, 16, new int[] { }, 6)]
    [InlineData(6, 6, new[] { 5 }, 6)]
    [InlineData(6, 7, new[] { 6, 7 }, null)]
    [InlineData(6, 5, new int[] { }, null)]
    public void SelectProbeCurrent_ReturnsLowestOfferableCurrentNoCarReports(int minCurrent, int maxCurrent, int[] pilotCurrents, int? expected)
    {
        var result = PilotCurrentProbeService.SelectProbeCurrent(minCurrent, maxCurrent, pilotCurrents.Select(p => (int?)p));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void SelectProbeCurrent_UsesMinimumProbeCurrent_WhenConnectorHasNoMinCurrent()
    {
        Assert.Equal(6, PilotCurrentProbeService.SelectProbeCurrent(null, 16, new int?[] { null }));
    }

    [Fact]
    public void SelectProbeCurrent_ReturnsNull_WhenConnectorHasNoMaxCurrent()
    {
        Assert.Null(PilotCurrentProbeService.SelectProbeCurrent(6, null, Array.Empty<int?>()));
    }

    [Theory]
    [InlineData(CarType.Tesla, true, false, true, false, true)]
    [InlineData(CarType.Tesla, false, true, true, true, true)]
    [InlineData(CarType.Tesla, false, true, true, false, false)]
    [InlineData(CarType.Tesla, false, false, true, true, false)]
    [InlineData(CarType.Tesla, true, true, false, true, false)]
    [InlineData(CarType.Manual, true, true, true, true, false)]
    [InlineData(CarType.SmartCar, true, false, true, true, false)]
    public void CanObservePilotCurrent_OnlyForTeslasWithPushedOrOnDemandData(CarType carType, bool useFleetTelemetry, bool useBle,
        bool vehicleDataFromTesla, bool vehicleDataViaBle, bool expected)
    {
        var configurationWrapper = new Mock<IConfigurationWrapper>();
        configurationWrapper.Setup(c => c.GetVehicleDataFromTesla()).Returns(vehicleDataFromTesla);
        configurationWrapper.Setup(c => c.GetVehicleDataViaBle()).Returns(vehicleDataViaBle);

        var result = PilotCurrentProbeService.CanObservePilotCurrent(carType, useFleetTelemetry, useBle, configurationWrapper.Object);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ProbeUnassignedConnectors_AssignsTheOnlyReactingCar()
    {
        await SeedConnector(ConnectorId, CarA);
        var carA = await SeedCar(CarA);
        SetUnassigned(ConnectorId);
        AddPluggedInConnectorState(ConnectorId);
        ReactOnProbe(ConnectorId, carA);

        await CreateService().ProbeUnassignedConnectors(CancellationToken.None);

        //The probe current, then the connector's max current for the car that was stopped via the Tesla API or BLE.
        Assert.Equal(new List<int> { 6, 16 }, _startedCurrents);
        Assert.Equal(CarA, _settings.ConfirmedLoadPointCarCombinations[ConnectorId].carId);
        Assert.Equal(CurrentFakeDate, _settings.LastPilotCurrentProbes[ConnectorId]);
        Mock.Mock<ITeslaService>().Verify(t => t.StopCharging(CarA), Times.Once);
        Mock.Mock<IOcppChargePointActionService>().Verify(a => a.StopCharging(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProbeUnassignedConnectors_OpensConnectorToMax_OnlyAfterTheMatchedCarStoppedCharging()
    {
        await SeedConnector(ConnectorId, CarA);
        var carA = await SeedCar(CarA);
        SetUnassigned(ConnectorId);
        AddPluggedInConnectorState(ConnectorId);
        ReactOnProbe(ConnectorId, carA);
        carA.IsCharging.Update(_plugInTime, true);
        var isChargingWhenOpened = new List<bool?>();
        Mock.Mock<ITeslaService>().Setup(t => t.StopCharging(CarA))
            .Callback(() => carA.IsCharging.Update(CurrentFakeDate, false))
            .Returns(Task.CompletedTask);
        Mock.Mock<IOcppChargePointActionService>()
            .Setup(a => a.StartCharging(ConnectorId, 16m, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback(() => isChargingWhenOpened.Add(carA.IsCharging.Value))
            .ReturnsAsync(new Result<RemoteStartTransactionResponse?>(new RemoteStartTransactionResponse(), null, null));

        await CreateService().ProbeUnassignedConnectors(CancellationToken.None);

        Assert.Equal(new List<bool?> { false }, isChargingWhenOpened);
    }

    [Fact]
    public async Task ProbeUnassignedConnectors_StopsConnector_WhenMatchedCarDoesNotStopCharging()
    {
        await SeedConnector(ConnectorId, CarA);
        var carA = await SeedCar(CarA);
        SetUnassigned(ConnectorId);
        AddPluggedInConnectorState(ConnectorId);
        ReactOnProbe(ConnectorId, carA);
        carA.IsCharging.Update(_plugInTime, true);

        await CreateService().ProbeUnassignedConnectors(CancellationToken.None);

        Assert.Equal(new List<int> { 6 }, _startedCurrents);
        Mock.Mock<IOcppChargePointActionService>().Verify(a => a.StopCharging(ConnectorId, It.IsAny<CancellationToken>()), Times.Once);
        //The car still is the one on the connector.
        Assert.Equal(CarA, _settings.ConfirmedLoadPointCarCombinations[ConnectorId].carId);
    }

    [Fact]
    public async Task ProbeUnassignedConnectors_AssignsCarOnConnector_WhenAnotherCarPluggedIntoOtherWallboxAtTheSameTime()
    {
        await SeedConnector(ConnectorId, CarA, CarB);
        var carA = await SeedCar(CarA);
        var carB = await SeedCar(CarB);
        carB.ChargerPilotCurrent.Update(_plugInTime, 6);
        SetUnassigned(ConnectorId);
        AddPluggedInConnectorState(ConnectorId);
        ReactOnProbe(ConnectorId, carA);

        await CreateService().ProbeUnassignedConnectors(CancellationToken.None);

        //6 A is what the other wallbox already offers car B, so a car showing it would prove nothing.
        Assert.Equal(new List<int> { 7, 16 }, _startedCurrents);
        Assert.Equal(CarA, _settings.ConfirmedLoadPointCarCombinations[ConnectorId].carId);
        Assert.Equal(6, carB.ChargerPilotCurrent.Value);
    }

    [Fact]
    public async Task ProbeUnassignedConnectors_AssignsEachCarToItsConnector_WhenTwoCarsArePluggedIntoTwoConnectors()
    {
        await SeedConnector(ConnectorId, CarA, CarB);
        await SeedConnector(SecondConnectorId, CarA, CarB);
        var carA = await SeedCar(CarA);
        var carB = await SeedCar(CarB);
        SetUnassigned(ConnectorId, SecondConnectorId);
        AddPluggedInConnectorState(ConnectorId);
        AddPluggedInConnectorState(SecondConnectorId);
        ReactOnProbe(ConnectorId, carB);
        ReactOnProbe(SecondConnectorId, carA);

        await CreateService().ProbeUnassignedConnectors(CancellationToken.None);

        Assert.Equal(CarB, _settings.ConfirmedLoadPointCarCombinations[ConnectorId].carId);
        Assert.Equal(CarA, _settings.ConfirmedLoadPointCarCombinations[SecondConnectorId].carId);
    }

    [Fact]
    public async Task ProbeUnassignedConnectors_DoesNotProbeAgain_WhenAlreadyAssignedCarIsTheOnlyCandidateLeft()
    {
        await SeedConnector(ConnectorId, CarA);
        await SeedConnector(SecondConnectorId, CarA);
        var carA = await SeedCar(CarA);
        SetUnassigned(ConnectorId, SecondConnectorId);
        AddPluggedInConnectorState(ConnectorId);
        AddPluggedInConnectorState(SecondConnectorId);
        ReactOnProbe(ConnectorId, carA);

        await CreateService().ProbeUnassignedConnectors(CancellationToken.None);

        Assert.Equal(new List<int> { 6, 16 }, _startedCurrents);
        Assert.False(_settings.ConfirmedLoadPointCarCombinations.ContainsKey(SecondConnectorId));
    }

    [Fact]
    public async Task ProbeUnassignedConnectors_AssignsNothing_WhenNoCarReacts()
    {
        await SeedConnector(ConnectorId, CarA);
        await SeedCar(CarA);
        SetUnassigned(ConnectorId);
        AddPluggedInConnectorState(ConnectorId);

        await CreateService().ProbeUnassignedConnectors(CancellationToken.None);

        Assert.Single(_startedCurrents);
        Assert.Empty(_settings.ConfirmedLoadPointCarCombinations);
        Mock.Mock<IOcppChargePointActionService>().Verify(a => a.StopCharging(ConnectorId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProbeUnassignedConnectors_RefreshesBleDataOfEveryCandidateEveryFifthPoll()
    {
        await SeedConnector(ConnectorId, CarA, CarB);
        await SeedCar(CarA);
        await SeedCar(CarB);
        SetUnassigned(ConnectorId);
        AddPluggedInConnectorState(ConnectorId);

        await CreateService().ProbeUnassignedConnectors(CancellationToken.None);

        Mock.Mock<IBleVehicleDataService>().Verify(b => b.RefreshSingleCarData(CarA), Times.Exactly(6));
        Mock.Mock<IBleVehicleDataService>().Verify(b => b.RefreshSingleCarData(CarB), Times.Exactly(6));
    }

    [Fact]
    public async Task ProbeUnassignedConnectors_AssignsNothing_WhenTwoCarsReportTheProbeCurrent()
    {
        await SeedConnector(ConnectorId, CarA, CarB);
        var carA = await SeedCar(CarA);
        var carB = await SeedCar(CarB);
        SetUnassigned(ConnectorId);
        AddPluggedInConnectorState(ConnectorId);
        ReactOnProbe(ConnectorId, carA, carB);

        await CreateService().ProbeUnassignedConnectors(CancellationToken.None);

        Assert.Empty(_settings.ConfirmedLoadPointCarCombinations);
    }

    [Fact]
    public async Task ProbeUnassignedConnectors_DoesNotProbeTwice_ForTheSamePlugIn()
    {
        await SeedConnector(ConnectorId, CarA);
        await SeedCar(CarA);
        SetUnassigned(ConnectorId);
        AddPluggedInConnectorState(ConnectorId);
        var service = CreateService();

        await service.ProbeUnassignedConnectors(CancellationToken.None);
        await service.ProbeUnassignedConnectors(CancellationToken.None);

        Assert.Single(_startedCurrents);
    }

    [Fact]
    public async Task ProbeUnassignedConnectors_ProbesAgain_WhenACandidateWasPluggedInAfterTheLastProbe()
    {
        await SeedConnector(ConnectorId, CarA);
        var carA = await SeedCar(CarA);
        SetUnassigned(ConnectorId);
        AddPluggedInConnectorState(ConnectorId);
        _settings.LastPilotCurrentProbes[ConnectorId] = CurrentFakeDate.AddMinutes(-1);
        carA.PluggedIn.Update(CurrentFakeDate.AddSeconds(-30), false);
        carA.PluggedIn.Update(CurrentFakeDate.AddSeconds(-20), true);

        await CreateService().ProbeUnassignedConnectors(CancellationToken.None);

        Assert.Single(_startedCurrents);
    }

    [Fact]
    public async Task ProbeUnassignedConnectors_ProbesAgain_WhenTheConnectorWasPluggedInAfterTheLastProbe()
    {
        await SeedConnector(ConnectorId, CarA);
        await SeedCar(CarA);
        SetUnassigned(ConnectorId);
        _settings.LastPilotCurrentProbes[ConnectorId] = _plugInTime.AddSeconds(-1);
        AddPluggedInConnectorState(ConnectorId);

        await CreateService().ProbeUnassignedConnectors(CancellationToken.None);

        Assert.Single(_startedCurrents);
    }

    [Fact]
    public async Task ProbeUnassignedConnectors_DoesNotProbe_WhenUserSetConnectorToGuestCar()
    {
        await SeedConnector(ConnectorId, CarA);
        await SeedCar(CarA);
        SetUnassigned(ConnectorId);
        AddPluggedInConnectorState(ConnectorId);
        _settings.ConfirmedLoadPointCarCombinations[ConnectorId] = (null, _plugInTime.AddMinutes(1));

        await CreateService().ProbeUnassignedConnectors(CancellationToken.None);

        Assert.Empty(_startedCurrents);
    }

    [Fact]
    public async Task ProbeUnassignedConnectors_Probes_WhenGuestCarChoiceWasForAnEarlierPlugIn()
    {
        await SeedConnector(ConnectorId, CarA);
        await SeedCar(CarA);
        SetUnassigned(ConnectorId);
        AddPluggedInConnectorState(ConnectorId);
        _settings.ConfirmedLoadPointCarCombinations[ConnectorId] = (null, _plugInTime.AddMinutes(-1));

        await CreateService().ProbeUnassignedConnectors(CancellationToken.None);

        Assert.Single(_startedCurrents);
    }

    [Fact]
    public async Task ProbeUnassignedConnectors_DoesNotProbe_WhenConnectorAlreadyHasACar()
    {
        await SeedConnector(ConnectorId, CarA, CarB);
        await SeedCar(CarA);
        await SeedCar(CarB);
        Mock.Mock<ILoadPointManagementService>().Setup(l => l.GetCombinationsToManage())
            .ReturnsAsync(new HashSet<DtoLoadpointCombination> { new(CarA, ConnectorId), new(CarB, null), });
        AddPluggedInConnectorState(ConnectorId);

        await CreateService().ProbeUnassignedConnectors(CancellationToken.None);

        Assert.Empty(_startedCurrents);
    }

    [Fact]
    public async Task ProbeUnassignedConnectors_DoesNotProbe_WhenConnectorIsNotPluggedIn()
    {
        await SeedConnector(ConnectorId, CarA);
        await SeedCar(CarA);
        SetUnassigned(ConnectorId);
        _settings.OcppConnectorStates[ConnectorId] = new DtoOcppConnectorState();

        await CreateService().ProbeUnassignedConnectors(CancellationToken.None);

        Assert.Empty(_startedCurrents);
    }

    [Fact]
    public async Task ProbeUnassignedConnectors_DoesNotProbe_WhenConnectorHasNoState()
    {
        await SeedConnector(ConnectorId, CarA);
        await SeedCar(CarA);
        SetUnassigned(ConnectorId);

        await CreateService().ProbeUnassignedConnectors(CancellationToken.None);

        Assert.Empty(_startedCurrents);
    }

    [Theory]
    [InlineData(false, true, true, true, false)]
    [InlineData(true, false, true, true, false)]
    [InlineData(true, true, false, true, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, true, true, true, true)]
    public async Task ProbeUnassignedConnectors_OnlyProbesForObservableAllowedTeslasAtHomeAndPluggedIn(bool isHome, bool isPluggedIn,
        bool isAllowed, bool isObservable, bool expectedProbe)
    {
        await SeedConnector(ConnectorId, isAllowed ? new[] { CarA } : Array.Empty<int>());
        var carA = await SeedCar(CarA, useFleetTelemetry: isObservable, useBle: false);
        carA.IsHomeGeofence.Update(_plugInTime, isHome);
        carA.PluggedIn.Update(_plugInTime, isPluggedIn);
        SetUnassigned(ConnectorId);
        AddPluggedInConnectorState(ConnectorId);

        await CreateService().ProbeUnassignedConnectors(CancellationToken.None);

        Assert.Equal(expectedProbe, _startedCurrents.Count == 1);
    }

    [Fact]
    public async Task ProbeUnassignedConnectors_RestoresLastSetCurrent_WhenConnectorWasCharging()
    {
        await SeedConnector(ConnectorId, CarA);
        var carA = await SeedCar(CarA);
        SetUnassigned(ConnectorId);
        var state = AddPluggedInConnectorState(ConnectorId);
        state.IsCharging.Update(_plugInTime, true);
        state.LastSetCurrent.Update(_plugInTime, 10m);
        carA.ChargerPilotCurrent.Update(_plugInTime, 10);
        ReactOnProbe(ConnectorId, carA);

        await CreateService().ProbeUnassignedConnectors(CancellationToken.None);

        var actionService = Mock.Mock<IOcppChargePointActionService>();
        actionService.Verify(a => a.SetChargingCurrent(ConnectorId, 10m, null, It.IsAny<CancellationToken>()), Times.Once);
        actionService.Verify(a => a.StopCharging(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(CarA, _settings.ConfirmedLoadPointCarCombinations[ConnectorId].carId);
    }

    [Fact]
    public async Task ProbeUnassignedConnectors_DoesNotWaitOrRestore_WhenProbeCurrentCouldNotBeOffered()
    {
        await SeedConnector(ConnectorId, CarA);
        await SeedCar(CarA);
        SetUnassigned(ConnectorId);
        AddPluggedInConnectorState(ConnectorId);
        var actionService = Mock.Mock<IOcppChargePointActionService>();
        actionService.Setup(a => a.StartCharging(ConnectorId, It.IsAny<decimal>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<RemoteStartTransactionResponse?>(null, "Not connected", null));

        await CreateService().ProbeUnassignedConnectors(CancellationToken.None);

        actionService.Verify(a => a.StopCharging(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        Mock.Mock<IBleVehicleDataService>().Verify(b => b.RefreshSingleCarData(It.IsAny<int>()), Times.Never);
        Assert.Empty(_settings.ConfirmedLoadPointCarCombinations);
        Assert.True(_settings.LastPilotCurrentProbes.ContainsKey(ConnectorId));
    }

    [Fact]
    public async Task ProbeUnassignedConnectors_DoesNotProbe_WhenEveryOfferableCurrentIsAlreadyReported()
    {
        await SeedConnector(ConnectorId, new[] { CarA }, maxCurrent: 6);
        var carA = await SeedCar(CarA);
        carA.ChargerPilotCurrent.Update(_plugInTime, 6);
        SetUnassigned(ConnectorId);
        AddPluggedInConnectorState(ConnectorId);

        await CreateService().ProbeUnassignedConnectors(CancellationToken.None);

        Assert.Empty(_startedCurrents);
        Assert.True(_settings.LastPilotCurrentProbes.ContainsKey(ConnectorId));
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public async Task ProbeUnassignedConnectors_OnlyProbes_WhenProbeCurrentFitsIntoMaxCombinedCurrent(int currentUsedByOtherCar, bool expectedProbe)
    {
        await SeedConnector(ConnectorId, CarA);
        await SeedCar(CarA);
        SetUnassigned(ConnectorId);
        AddPluggedInConnectorState(ConnectorId);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.MaxCombinedCurrent()).Returns(16);
        SetChargingCurrents((CarB, null, currentUsedByOtherCar));

        var currentToReserve = await CreateService().ProbeUnassignedConnectors(CancellationToken.None);

        Assert.Equal(expectedProbe, _startedCurrents.Count == 1);
        Assert.Equal(expectedProbe ? 0 : 6, currentToReserve);
        Assert.Equal(expectedProbe, _settings.LastPilotCurrentProbes.ContainsKey(ConnectorId));
    }

    [Fact]
    public async Task ProbeUnassignedConnectors_ProbesPostponedConnector_OnceOtherLoadPointsReducedTheirCurrent()
    {
        await SeedConnector(ConnectorId, CarA);
        var carA = await SeedCar(CarA);
        SetUnassigned(ConnectorId);
        AddPluggedInConnectorState(ConnectorId);
        ReactOnProbe(ConnectorId, carA);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.MaxCombinedCurrent()).Returns(16);
        SetChargingCurrents((CarB, null, 16m));
        var service = CreateService();

        var firstReservation = await service.ProbeUnassignedConnectors(CancellationToken.None);
        SetChargingCurrents((CarB, null, 10m));
        var secondReservation = await service.ProbeUnassignedConnectors(CancellationToken.None);
        var thirdReservation = await service.ProbeUnassignedConnectors(CancellationToken.None);

        Assert.Equal(6, firstReservation);
        Assert.Equal(0, secondReservation);
        Assert.Equal(0, thirdReservation);
        Assert.Equal(new List<int> { 6, 16 }, _startedCurrents);
        Assert.Equal(CarA, _settings.ConfirmedLoadPointCarCombinations[ConnectorId].carId);
    }

    [Fact]
    public async Task ProbeUnassignedConnectors_DoesNotCountCurrentOfTheProbedConnector()
    {
        await SeedConnector(ConnectorId, CarA);
        await SeedCar(CarA);
        SetUnassigned(ConnectorId);
        AddPluggedInConnectorState(ConnectorId);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.MaxCombinedCurrent()).Returns(16);
        //A guest car charging on the probed connector: the probe replaces its current instead of adding to it.
        SetChargingCurrents((null, ConnectorId, 16m));

        var currentToReserve = await CreateService().ProbeUnassignedConnectors(CancellationToken.None);

        Assert.Single(_startedCurrents);
        Assert.Equal(0, currentToReserve);
    }

    [Fact]
    public async Task ProbeUnassignedConnectors_ReservesTheLargestPostponedProbeCurrent()
    {
        await SeedConnector(ConnectorId, CarA);
        await SeedConnector(SecondConnectorId, CarB);
        await SeedCar(CarA);
        var carB = await SeedCar(CarB);
        carB.ChargerPilotCurrent.Update(_plugInTime, 6);
        SetUnassigned(ConnectorId, SecondConnectorId);
        AddPluggedInConnectorState(ConnectorId);
        AddPluggedInConnectorState(SecondConnectorId);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.MaxCombinedCurrent()).Returns(16);
        SetChargingCurrents((99, null, 16m));

        var currentToReserve = await CreateService().ProbeUnassignedConnectors(CancellationToken.None);

        Assert.Empty(_startedCurrents);
        Assert.Equal(7, currentToReserve);
    }

    [Fact]
    public async Task ProbeUnassignedConnectors_ReservesNothing_WhenNoConnectorNeedsAProbe()
    {
        await SeedConnector(ConnectorId, CarA);
        await SeedCar(CarA);
        SetUnassigned(ConnectorId);
        AddPluggedInConnectorState(ConnectorId);
        _settings.ConfirmedLoadPointCarCombinations[ConnectorId] = (null, _plugInTime.AddMinutes(1));
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.MaxCombinedCurrent()).Returns(16);
        SetChargingCurrents((CarB, null, 16m));

        var currentToReserve = await CreateService().ProbeUnassignedConnectors(CancellationToken.None);

        Assert.Equal(0, currentToReserve);
    }

    private void SetChargingCurrents(params (int? carId, int? connectorId, decimal current)[] loadPoints)
    {
        Mock.Mock<ILoadPointManagementService>().Setup(l => l.GetLoadPointsWithChargingDetails())
            .ReturnsAsync(loadPoints
                .Select(l => new DtoLoadPointWithCurrentChargingValues { CarId = l.carId, ChargingConnectorId = l.connectorId, ChargingCurrent = l.current, })
                .ToList());
    }

    private PilotCurrentProbeService CreateService()
    {
        var service = Mock.Create<PilotCurrentProbeService>();
        service.PollInterval = TimeSpan.Zero;
        return service;
    }

    private Task SeedConnector(int connectorId, params int[] allowedCarIds) => SeedConnector(connectorId, allowedCarIds, 16);

    private async Task SeedConnector(int connectorId, int[] allowedCarIds, int maxCurrent)
    {
        var connector = new OcppChargingStationConnector($"Connector {connectorId}")
        {
            Id = connectorId,
            ConnectorId = 1,
            ShouldBeManaged = true,
            MinCurrent = 6,
            MaxCurrent = maxCurrent,
            ConnectedPhasesCount = 3,
        };
        foreach (var carId in allowedCarIds)
        {
            connector.AllowedCars.Add(new ChargingStationConnectorAllowedCar { CarId = carId, OcppChargingStationConnectorId = connectorId, });
        }
        Context.OcppChargingStationConnectors.Add(connector);
        await Context.SaveChangesAsync();
        DetachAllEntities();
    }

    private async Task<DtoCar> SeedCar(int carId, bool useFleetTelemetry = true, bool useBle = false)
    {
        Context.Cars.Add(new Car
        {
            Id = carId,
            Vin = $"VIN{carId}",
            CarType = CarType.Tesla,
            ShouldBeManaged = true,
            UseFleetTelemetry = useFleetTelemetry,
            UseBle = useBle,
        });
        await Context.SaveChangesAsync();
        DetachAllEntities();
        var dtoCar = new DtoCar { Id = carId, Vin = $"VIN{carId}", };
        dtoCar.IsHomeGeofence.Update(_plugInTime, true);
        dtoCar.PluggedIn.Update(_plugInTime, true);
        dtoCar.ChargerPilotCurrent.Update(_plugInTime, 5);
        _settings.Cars.Add(dtoCar);
        return dtoCar;
    }

    private void SetUnassigned(params int[] connectorIds)
    {
        var combinations = connectorIds.Select(id => new DtoLoadpointCombination(null, id)).ToHashSet();
        foreach (var car in _settings.Cars)
        {
            combinations.Add(new DtoLoadpointCombination(car.Id, null));
        }
        Mock.Mock<ILoadPointManagementService>().Setup(l => l.GetCombinationsToManage()).ReturnsAsync(combinations);
    }

    private DtoOcppConnectorState AddPluggedInConnectorState(int connectorId)
    {
        var state = new DtoOcppConnectorState();
        state.IsPluggedIn.Update(_plugInTime, true);
        _settings.OcppConnectorStates[connectorId] = state;
        return state;
    }

    /// <summary>
    /// Makes the given cars report whatever current the given connector is told to offer, like a car plugged into it.
    /// </summary>
    private void ReactOnProbe(int connectorId, params DtoCar[] cars)
    {
        Mock.Mock<IOcppChargePointActionService>()
            .Setup(a => a.StartCharging(connectorId, It.IsAny<decimal>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback((int _, decimal current, int? _, CancellationToken _) =>
            {
                _startedCurrents.Add((int)current);
                foreach (var car in cars)
                {
                    car.ChargerPilotCurrent.Update(CurrentFakeDate, (int)current);
                }
            })
            .ReturnsAsync(new Result<RemoteStartTransactionResponse?>(new RemoteStartTransactionResponse(), null, null));
    }
}
