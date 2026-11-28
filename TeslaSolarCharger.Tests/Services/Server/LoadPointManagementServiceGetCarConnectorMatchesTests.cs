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
using TeslaSolarCharger.Server.Services.ChargepointAction;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class LoadPointManagementServiceGetCarConnectorMatchesTests : TestBase
{
    private const int ConnectorId = 2;
    private const int CarId = 10;
    private const int ManualCarId = 11;

    private readonly Settings _settings = new();
    private readonly DateTimeOffset _plugInTime;

    public LoadPointManagementServiceGetCarConnectorMatchesTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
        _plugInTime = CurrentFakeDate.AddMinutes(-5);
        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.Setup(s => s.Cars).Returns(_settings.Cars);
        settingsMock.Setup(s => s.OcppConnectorStates).Returns(_settings.OcppConnectorStates);
        settingsMock.Setup(s => s.ConfirmedLoadPointCarCombinations).Returns(_settings.ConfirmedLoadPointCarCombinations);
        settingsMock.Setup(s => s.LastPilotCurrentProbes).Returns(_settings.LastPilotCurrentProbes);
        var configurationWrapper = Mock.Mock<IConfigurationWrapper>();
        configurationWrapper.Setup(c => c.MaxPluggedInTimeDifferenceToMatchCarAndOcppConnector()).Returns(TimeSpan.FromSeconds(30));
        configurationWrapper.Setup(c => c.GetVehicleDataFromTesla()).Returns(true);
        configurationWrapper.Setup(c => c.GetVehicleDataViaBle()).Returns(true);
    }

    [Theory]
    [InlineData(true, false, null)]
    [InlineData(false, true, null)]
    [InlineData(false, false, CarId)]
    public async Task GetCarConnectorMatches_MatchesTeslaByPlugInTime_OnlyIfItsPilotCurrentCanNotBeObserved(bool useFleetTelemetry, bool useBle,
        int? expectedCarId)
    {
        await Seed(CarType.Tesla, useFleetTelemetry, useBle);

        var matches = await Mock.Create<LoadPointManagementService>()
            .GetCarConnectorMatches(new[] { CarId }, new[] { ConnectorId }, false);

        Assert.Equal(expectedCarId, matches.Single(m => m.ChargingConnectorId == ConnectorId).CarId);
    }

    [Fact]
    public async Task GetCarConnectorMatches_MatchesProbedTesla_FromConfirmedCombination()
    {
        await Seed(CarType.Tesla, true, false);
        _settings.ConfirmedLoadPointCarCombinations[ConnectorId] = (CarId, CurrentFakeDate);

        var matches = await Mock.Create<LoadPointManagementService>()
            .GetCarConnectorMatches(new[] { CarId }, new[] { ConnectorId }, false);

        Assert.Equal(CarId, matches.Single(m => m.ChargingConnectorId == ConnectorId).CarId);
    }

    [Fact]
    public async Task GetCarConnectorMatches_DropsConfirmedCombination_WhenCarWasPluggedInAgainSince()
    {
        await Seed(CarType.Tesla, true, false);
        _settings.ConfirmedLoadPointCarCombinations[ConnectorId] = (CarId, _plugInTime.AddSeconds(20));
        var car = _settings.Cars.Single();
        car.PluggedIn.Update(_plugInTime.AddMinutes(1), false);
        car.PluggedIn.Update(_plugInTime.AddMinutes(2), true);

        var matches = await Mock.Create<LoadPointManagementService>()
            .GetCarConnectorMatches(new[] { CarId }, new[] { ConnectorId }, false);

        Assert.Null(matches.Single(m => m.ChargingConnectorId == ConnectorId).CarId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-60)]
    public async Task GetCarConnectorMatches_LeavesOnlyAllowedTeslaToTheProbe_WhenGuestCarsAreNotAllowed(int carPluggedInMinutesBeforeConnector)
    {
        //"No guest cars" is no guarantee: the Tesla can be plugged into another wallbox while another car uses this connector.
        await Seed(CarType.Tesla, true, false, false);
        var car = _settings.Cars.Single();
        car.PluggedIn.Update(_plugInTime.AddMinutes(carPluggedInMinutesBeforeConnector - 1), false, true);
        car.PluggedIn.Update(_plugInTime.AddMinutes(carPluggedInMinutesBeforeConnector), true, true);

        var matches = await Mock.Create<LoadPointManagementService>()
            .GetCarConnectorMatches(new[] { CarId }, new[] { ConnectorId }, false);

        Assert.Null(matches.Single(m => m.ChargingConnectorId == ConnectorId).CarId);
        Assert.Contains(matches, m => m.CarId == CarId && m.ChargingConnectorId == null);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ChargingConnector_IsAssignedToPluggedInTesla_WhenOnlyAllowedCarsAreThatTeslaAndOneOtherCar(bool useFleetTelemetry, bool useBle)
    {
        //Without guest cars and with one other allowed car, a plugged in Tesla must still get the connector (as in v2.49.0).
        await Seed(CarType.Tesla, useFleetTelemetry, useBle, false);
        await AddAllowedCar(ManualCarId, CarType.Manual);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.MaxCombinedCurrent()).Returns(int.MaxValue);
        Mock.Mock<IDateTimeProvider>().Setup(d => d.DateTimeOffSetUtcNow()).Returns(CurrentFakeDate);
        var tesla = _settings.Cars.Single(c => c.Id == CarId);
        tesla.ChargerPilotCurrent.Update(_plugInTime, 5);
        LetTeslaReportOfferedCurrent(tesla);
        var loadPointManagementService = Mock.Create<LoadPointManagementService>();
        var carIds = new[] { CarId, ManualCarId, };

        //One charging cycle: matching, then probing the connectors whose car matching could only guess or not find.
        var matches = await loadPointManagementService.GetCarConnectorMatches(carIds, new[] { ConnectorId }, false);
        Mock.Mock<ILoadPointManagementService>().Setup(l => l.GetCombinationsToManage()).ReturnsAsync(matches);
        Mock.Mock<ILoadPointManagementService>().Setup(l => l.GetLoadPointsWithChargingDetails())
            .ReturnsAsync(new List<DtoLoadPointWithCurrentChargingValues>());
        var probeService = Mock.Create<PilotCurrentProbeService>();
        probeService.PollInterval = TimeSpan.Zero;
        await probeService.ProbeUnconfirmedConnectors(CancellationToken.None);
        matches = await loadPointManagementService.GetCarConnectorMatches(carIds, new[] { ConnectorId }, false);

        Assert.Equal(CarId, matches.Single(m => m.ChargingConnectorId == ConnectorId).CarId);
        Assert.Contains(matches, m => m.CarId == ManualCarId && m.ChargingConnectorId == null);
    }

    private async Task AddAllowedCar(int carId, CarType carType)
    {
        Context.Cars.Add(new Car { Id = carId, Vin = $"VIN{carId}", CarType = carType, ShouldBeManaged = true, });
        Context.ChargingStationConnectorAllowedCars.Add(new ChargingStationConnectorAllowedCar { CarId = carId, OcppChargingStationConnectorId = ConnectorId, });
        await Context.SaveChangesAsync();
        DetachAllEntities();
        _settings.Cars.Add(new DtoCar { Id = carId, Vin = $"VIN{carId}", });
    }

    /// <summary>
    /// Makes the Tesla report whatever current the connector is told to offer, like a car plugged into it.
    /// </summary>
    private void LetTeslaReportOfferedCurrent(DtoCar tesla)
    {
        var actionService = Mock.Mock<IOcppChargePointActionService>();
        actionService.Setup(a => a.StartCharging(ConnectorId, It.IsAny<decimal>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback((int _, decimal current, int? _, CancellationToken _) => tesla.ChargerPilotCurrent.Update(CurrentFakeDate, (int)current))
            .ReturnsAsync(new Result<RemoteStartTransactionResponse?>(new RemoteStartTransactionResponse(), null, null));
        actionService.Setup(a => a.StopCharging(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<RemoteStopTransactionResponse?>(new RemoteStopTransactionResponse(), null, null));
    }

    private async Task Seed(CarType carType, bool useFleetTelemetry, bool useBle, bool allowGuestCars = true)
    {
        Context.Cars.Add(new Car
        {
            Id = CarId,
            Vin = "VIN",
            CarType = carType,
            ShouldBeManaged = true,
            UseFleetTelemetry = useFleetTelemetry,
            UseBle = useBle,
        });
        var connector = new OcppChargingStationConnector("Connector")
        {
            Id = ConnectorId,
            ConnectorId = 1,
            ShouldBeManaged = true,
            AllowGuestCars = allowGuestCars,
            MinCurrent = 6,
            MaxCurrent = 16,
            ConnectedPhasesCount = 3,
        };
        connector.AllowedCars.Add(new ChargingStationConnectorAllowedCar { CarId = CarId, OcppChargingStationConnectorId = ConnectorId, });
        Context.OcppChargingStationConnectors.Add(connector);
        await Context.SaveChangesAsync();
        DetachAllEntities();

        var dtoCar = new DtoCar { Id = CarId, Vin = "VIN", };
        dtoCar.IsHomeGeofence.Update(_plugInTime, true);
        dtoCar.PluggedIn.Update(_plugInTime.AddSeconds(-1), false);
        dtoCar.PluggedIn.Update(_plugInTime, true);
        _settings.Cars.Add(dtoCar);

        var connectorState = new DtoOcppConnectorState();
        connectorState.IsPluggedIn.Update(_plugInTime.AddSeconds(10), true);
        _settings.OcppConnectorStates[ConnectorId] = connectorState;
    }
}
