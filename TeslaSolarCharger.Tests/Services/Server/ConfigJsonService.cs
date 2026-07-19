using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeslaSolarCharger.Server.Dtos.FleetTelemetry;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using Xunit;


namespace TeslaSolarCharger.Tests.Services.Server;

public class ConfigJsonService : TestBase
{
    private const string TestVin = "TESTVIN123456789A";

    public ConfigJsonService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public async Task SwitchesCarToBleDataCollectionOnManualSave()
    {
        SetupCarForBleDataCollectionTests();
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.GetVehicleDataViaBle()).Returns(true);
        Mock.Mock<IFleetTelemetryConfigurationService>()
            .Setup(f => f.DeleteFleetTelemetryConfiguration(TestVin))
            .ReturnsAsync(new DtoFleetTelemetryConfigurationResult { Success = true });

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ConfigJsonService>();
        await service.UpdateCarBasicConfiguration(1, GenerateBleCarBasicConfiguration());

        var databaseCar = Context.Cars.Single(c => c.Id == 1);
        Assert.False(databaseCar.UseFleetTelemetry);
        Assert.Equal(HomeDetectionVia.BlePresence, databaseCar.HomeDetectionVia);
        Mock.Mock<IFleetTelemetryConfigurationService>()
            .Verify(f => f.DeleteFleetTelemetryConfiguration(TestVin), Times.Once);
        Mock.Mock<IFleetTelemetryConfigurationService>()
            .Verify(f => f.SetFleetTelemetryConfiguration(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task DoesNotSwitchToBleDataCollectionWhenGetVehicleDataViaBleIsDisabled()
    {
        SetupCarForBleDataCollectionTests();
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.GetVehicleDataViaBle()).Returns(false);
        Mock.Mock<IFleetTelemetryConfigurationService>()
            .Setup(f => f.SetFleetTelemetryConfiguration(TestVin, false))
            .ReturnsAsync(new DtoFleetTelemetryConfigurationResult { Success = true });

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ConfigJsonService>();
        await service.UpdateCarBasicConfiguration(1, GenerateBleCarBasicConfiguration());

        var databaseCar = Context.Cars.Single(c => c.Id == 1);
        Assert.True(databaseCar.UseFleetTelemetry);
        Assert.Equal(HomeDetectionVia.LocatedAtHome, databaseCar.HomeDetectionVia);
        Mock.Mock<IFleetTelemetryConfigurationService>()
            .Verify(f => f.DeleteFleetTelemetryConfiguration(It.IsAny<string>()), Times.Never);
        Mock.Mock<IFleetTelemetryConfigurationService>()
            .Verify(f => f.SetFleetTelemetryConfiguration(TestVin, false), Times.Once);
    }

    [Fact]
    public async Task DoesNotSwitchToBleDataCollectionWithTrackingRelevantFields()
    {
        SetupCarForBleDataCollectionTests();
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.GetVehicleDataViaBle()).Returns(true);
        Mock.Mock<IFleetTelemetryConfigurationService>()
            .Setup(f => f.SetFleetTelemetryConfiguration(TestVin, false))
            .ReturnsAsync(new DtoFleetTelemetryConfigurationResult { Success = true });

        var carBasicConfiguration = GenerateBleCarBasicConfiguration();
        carBasicConfiguration.IncludeTrackingRelevantFields = true;
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ConfigJsonService>();
        await service.UpdateCarBasicConfiguration(1, carBasicConfiguration);

        var databaseCar = Context.Cars.Single(c => c.Id == 1);
        Assert.True(databaseCar.UseFleetTelemetry);
        Assert.Equal(HomeDetectionVia.LocatedAtHome, databaseCar.HomeDetectionVia);
        Mock.Mock<IFleetTelemetryConfigurationService>()
            .Verify(f => f.DeleteFleetTelemetryConfiguration(It.IsAny<string>()), Times.Never);
        Mock.Mock<IFleetTelemetryConfigurationService>()
            .Verify(f => f.SetFleetTelemetryConfiguration(TestVin, false), Times.Once);
    }

    private void SetupCarForBleDataCollectionTests()
    {
        Context.Cars.Add(new TeslaSolarCharger.Model.Entities.TeslaSolarCharger.Car
        {
            Id = 1,
            Vin = TestVin,
            CarType = CarType.Tesla,
            ShouldBeManaged = true,
            UseBle = true,
            UseFleetTelemetry = true,
            IncludeTrackingRelevantFields = false,
            HomeDetectionVia = HomeDetectionVia.LocatedAtHome,
        });
        Context.SaveChangesAsync().GetAwaiter().GetResult();
        DetachAllEntities();
        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar> { new() { Id = 1, Vin = TestVin } });
    }

    private static CarBasicConfiguration GenerateBleCarBasicConfiguration()
    {
        return new CarBasicConfiguration(1, "Test Car")
        {
            Vin = TestVin,
            CarType = CarType.Tesla,
            ShouldBeManaged = true,
            UseBle = true,
            BleApiBaseUrl = "http://ble-container:7210",
            //The UI normally already disables Fleet Telemetry for BLE data collection cars, but the server must also
            //handle stale clients that still send true.
            UseFleetTelemetry = true,
            IncludeTrackingRelevantFields = false,
            HomeDetectionVia = HomeDetectionVia.LocatedAtHome,
            MinimumAmpere = 6,
            MaximumAmpere = 16,
            UsableEnergy = 75,
            ChargingPriority = 1,
            MaximumPhases = 3,
        };
    }

    //ToDo: need to be able to handle vins instead of IDs
    //[Fact]
    //public void Adds_every_new_car()
    //{
    //    var newCarIds = new List<int>() { 1, 2, 3, 4 };
    //    var cars = new List<DtoCar>();

    //    var configJsonService = Mock.Create<TeslaSolarCharger.Server.Services.ConfigJsonService>();
    //    configJsonService.AddNewCars(newCarIds, cars);

    //    Assert.Equal(newCarIds.Count, cars.Count);
    //}

    //[Fact]
    //public void Sets_correct_default_values_on_new_cars()
    //{
    //    var newCarIds = new List<int>() { 1, 2, 3, 4 };
    //    var cars = new List<DtoCar>();

    //    var configJsonService = Mock.Create<TeslaSolarCharger.Server.Services.ConfigJsonService>();
    //    configJsonService.AddNewCars(newCarIds, cars);

    //    foreach (var car in cars)
    //    {
    //        Assert.Equal(ChargeMode.PvAndMinSoc, car.CarConfiguration.ChargeMode);
    //        Assert.Equal(16, car.CarConfiguration.MaximumAmpere);
    //        Assert.Equal(1, car.CarConfiguration.MinimumAmpere);
    //        Assert.Equal(75, car.CarConfiguration.UsableEnergy);
    //        Assert.Null(car.CarState.ShouldStartChargingSince);
    //        Assert.Null(car.CarState.ShouldStopChargingSince);
    //    }
    //}

    //[Fact]
    //public void Removes_old_cars()
    //{
    //    var newCarIds = new List<int>() { 1, 2, 3, 4 };
    //    var cars = new List<DtoCar>();

    //    var configJsonService = Mock.Create<TeslaSolarCharger.Server.Services.ConfigJsonService>();
    //    configJsonService.AddNewCars(newCarIds, cars);

    //    configJsonService.RemoveOldCars(cars, new List<int>() { 1, 3 });

    //    Assert.Contains(cars, car => car.Id == 1);
    //    Assert.Contains(cars, car => car.Id == 3);
    //    Assert.DoesNotContain(cars, car => car.Id == 2);
    //    Assert.DoesNotContain(cars, car => car.Id == 4);
    //}

}
