using System;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services;
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

    private async Task Seed(CarType carType, bool useFleetTelemetry, bool useBle)
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
            AllowGuestCars = true,
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
