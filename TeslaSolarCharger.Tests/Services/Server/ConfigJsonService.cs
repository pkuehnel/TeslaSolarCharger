using Autofac;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeslaSolarCharger.Server.Dtos.FleetTelemetry;
using TeslaSolarCharger.Server.Helper;
using TeslaSolarCharger.Server.Helper.Contracts;
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

    [Fact]
    public async Task StoresTheSelectedBluetoothAdapterOnTheCar()
    {
        SetupCarForBleDataCollectionTests();
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.GetVehicleDataViaBle()).Returns(true);
        Mock.Mock<IFleetTelemetryConfigurationService>()
            .Setup(f => f.DeleteFleetTelemetryConfiguration(TestVin))
            .ReturnsAsync(new DtoFleetTelemetryConfigurationResult { Success = true });

        var carBasicConfiguration = GenerateBleCarBasicConfiguration();
        carBasicConfiguration.BleAdapterAddress = "AA:BB:CC:DD:EE:FF";
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ConfigJsonService>();
        await service.UpdateCarBasicConfiguration(1, carBasicConfiguration);

        //Both the database and the in memory car must carry the selection: the database survives restarts, the in
        //memory car is what every BLE request reads to decide which adapter to use.
        Assert.Equal("AA:BB:CC:DD:EE:FF", Context.Cars.Single(c => c.Id == 1).BleAdapterAddress);
        Assert.Equal("AA:BB:CC:DD:EE:FF", Mock.Mock<ISettings>().Object.Cars.Single(c => c.Id == 1).BleAdapterAddress);
    }

    [Fact]
    public async Task ClearingTheAdapterSelectionFallsBackToTheContainerDefault()
    {
        SetupCarForBleDataCollectionTests(bleAdapterAddress: "AA:BB:CC:DD:EE:FF");
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.GetVehicleDataViaBle()).Returns(true);
        Mock.Mock<IFleetTelemetryConfigurationService>()
            .Setup(f => f.DeleteFleetTelemetryConfiguration(TestVin))
            .ReturnsAsync(new DtoFleetTelemetryConfigurationResult { Success = true });

        var carBasicConfiguration = GenerateBleCarBasicConfiguration();
        carBasicConfiguration.BleAdapterAddress = null;
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ConfigJsonService>();
        await service.UpdateCarBasicConfiguration(1, carBasicConfiguration);

        Assert.Null(Context.Cars.Single(c => c.Id == 1).BleAdapterAddress);
    }

    [Theory]
    [InlineData(CarValueType.ModuleTempMin, nameof(DtoCar.MinBatteryTemperature), 18.5, 21.5)]
    [InlineData(CarValueType.ModuleTempMax, nameof(DtoCar.MaxBatteryTemperature), 19.5, 23.5)]
    [InlineData(CarValueType.ChargeAmps, nameof(DtoCar.ChargerActualCurrent), 6, 16)]
    [InlineData(CarValueType.ChargeCurrentRequest, nameof(DtoCar.ChargerRequestedCurrent), 8, 13)]
    [InlineData(CarValueType.IsPluggedIn, nameof(DtoCar.PluggedIn), false, true)]
    [InlineData(CarValueType.IsCharging, nameof(DtoCar.IsCharging), true, false)]
    [InlineData(CarValueType.ChargerPilotCurrent, nameof(DtoCar.ChargerPilotCurrent), 16, 32)]
    [InlineData(CarValueType.Longitude, nameof(DtoCar.Longitude), 11.5, 11.6)]
    [InlineData(CarValueType.Latitude, nameof(DtoCar.Latitude), 48.1, 48.2)]
    [InlineData(CarValueType.StateOfCharge, nameof(DtoCar.SoC), 55, 60)]
    [InlineData(CarValueType.StateOfChargeLimit, nameof(DtoCar.SocLimit), 80, 90)]
    [InlineData(CarValueType.ChargerPhases, nameof(DtoCar.ChargerPhases), 1, 3)]
    [InlineData(CarValueType.ChargerVoltage, nameof(DtoCar.ChargerVoltage), 229, 231)]
    public async Task RestoresTheLatestLoggedValueOfEveryCarProperty(CarValueType carValueType, string dtoCarPropertyName,
        object olderValue, object latestValue)
    {
        AddCarForCarValueLogTests(1);
        //The latest value is inserted first so the result can not depend on the insertion order.
        AddCarValueLogs(
            CreateCarValueLog(1, carValueType, LatestLogTimestamp, latestValue),
            CreateCarValueLog(1, carValueType, OlderLogTimestamp, olderValue));

        var car = (await LoadCarsIntoSettings()).Single();

        dynamic restoredValue = typeof(DtoCar).GetProperty(dtoCarPropertyName)!.GetValue(car)!;
        Assert.Equal(latestValue, (object?)restoredValue.Value);
        Assert.Equal(new DateTimeOffset(LatestLogTimestamp, TimeSpan.Zero), (DateTimeOffset)restoredValue.Timestamp);
    }

    [Fact]
    public async Task KeepsTheTimeOfTheLastValueChange()
    {
        var firstChange = OlderLogTimestamp.AddHours(-1);
        AddCarForCarValueLogTests(1);
        AddCarValueLogs(
            CreateCarValueLog(1, CarValueType.StateOfCharge, firstChange, 50),
            CreateCarValueLog(1, CarValueType.StateOfCharge, OlderLogTimestamp, 60),
            CreateCarValueLog(1, CarValueType.StateOfCharge, LatestLogTimestamp, 60));

        var car = (await LoadCarsIntoSettings()).Single();

        Assert.Equal(60, car.SoC.Value);
        Assert.Equal(new DateTimeOffset(LatestLogTimestamp, TimeSpan.Zero), car.SoC.Timestamp);
        Assert.Equal(new DateTimeOffset(OlderLogTimestamp, TimeSpan.Zero), car.SoC.LastChanged);
    }

    [Fact]
    public async Task RestoresASingleLoggedValue()
    {
        AddCarForCarValueLogTests(1);
        AddCarValueLogs(CreateCarValueLog(1, CarValueType.StateOfCharge, LatestLogTimestamp, 42));

        var car = (await LoadCarsIntoSettings()).Single();

        Assert.Equal(42, car.SoC.Value);
        Assert.Equal(new DateTimeOffset(LatestLogTimestamp, TimeSpan.Zero), car.SoC.Timestamp);
    }

    [Fact]
    public async Task OnlyRestoresValuesLoggedForTheSameCar()
    {
        AddCarForCarValueLogTests(1);
        AddCarForCarValueLogTests(2);
        AddCarValueLogs(
            CreateCarValueLog(1, CarValueType.StateOfCharge, OlderLogTimestamp, 50),
            CreateCarValueLog(2, CarValueType.StateOfCharge, LatestLogTimestamp, 90));

        var cars = await LoadCarsIntoSettings();

        Assert.Equal(50, cars.Single(c => c.Id == 1).SoC.Value);
        Assert.Equal(90, cars.Single(c => c.Id == 2).SoC.Value);
    }

    [Fact]
    public async Task KeepsDefaultValuesForCarsWithoutLoggedValues()
    {
        AddCarForCarValueLogTests(1);
        AddCarForCarValueLogTests(2);
        AddCarValueLogs(CreateCarValueLog(2, CarValueType.StateOfCharge, LatestLogTimestamp, 90));

        var car = (await LoadCarsIntoSettings()).Single(c => c.Id == 1);

        Assert.Null(car.SoC.Value);
        Assert.Equal(DateTimeOffset.MinValue, car.SoC.Timestamp);
        Assert.Null(car.PluggedIn.Value);
        Assert.Null(car.IsOnline.Value);
        Assert.Null(car.IsHomeGeofence.Value);
    }

    [Fact]
    public async Task RestoresTheOnlineStateFromTheLatestAsleepOrOfflineValue()
    {
        AddCarForCarValueLogTests(1);
        AddCarValueLogs(
            CreateCarValueLog(1, CarValueType.AsleepOrOffline, OlderLogTimestamp, true),
            CreateCarValueLog(1, CarValueType.AsleepOrOffline, LatestLogTimestamp, false));

        var car = (await LoadCarsIntoSettings()).Single();

        Assert.True(car.IsOnline.Value);
        Assert.Equal(new DateTimeOffset(LatestLogTimestamp, TimeSpan.Zero), car.IsOnline.Timestamp);
    }

    [Theory]
    [InlineData(CarValueType.LocatedAtHome, HomeDetectionVia.LocatedAtHome)]
    [InlineData(CarValueType.LocatedAtHome, HomeDetectionVia.BlePresence)]
    [InlineData(CarValueType.LocatedAtWork, HomeDetectionVia.LocatedAtWork)]
    [InlineData(CarValueType.LocatedAtFavorite, HomeDetectionVia.LocatedAtFavorite)]
    public async Task RestoresHomePresenceFromTheLocationValueOfTheHomeDetection(CarValueType carValueType, HomeDetectionVia homeDetectionVia)
    {
        AddCarForCarValueLogTests(1, homeDetectionVia);
        AddCarValueLogs(
            CreateCarValueLog(1, carValueType, OlderLogTimestamp, false),
            CreateCarValueLog(1, carValueType, LatestLogTimestamp, true));

        var car = (await LoadCarsIntoSettings()).Single();

        Assert.True(car.IsHomeGeofence.Value);
        Assert.Equal(new DateTimeOffset(LatestLogTimestamp, TimeSpan.Zero), car.IsHomeGeofence.Timestamp);
    }

    [Fact]
    public async Task IgnoresLocationValuesThatAreNotUsedForHomeDetection()
    {
        AddCarForCarValueLogTests(1, HomeDetectionVia.LocatedAtHome);
        AddCarValueLogs(
            CreateCarValueLog(1, CarValueType.LocatedAtHome, OlderLogTimestamp, false),
            CreateCarValueLog(1, CarValueType.LocatedAtWork, LatestLogTimestamp, true),
            CreateCarValueLog(1, CarValueType.LocatedAtFavorite, LatestLogTimestamp, true));

        var car = (await LoadCarsIntoSettings()).Single();

        Assert.False(car.IsHomeGeofence.Value);
        Assert.Equal(new DateTimeOffset(OlderLogTimestamp, TimeSpan.Zero), car.IsHomeGeofence.Timestamp);
    }

    private static readonly DateTime OlderLogTimestamp = new(2026, 9, 17, 6, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LatestLogTimestamp = new(2026, 9, 17, 7, 0, 0, DateTimeKind.Utc);

    private void AddCarForCarValueLogTests(int carId, HomeDetectionVia homeDetectionVia = HomeDetectionVia.LocatedAtHome)
    {
        Context.Cars.Add(new TeslaSolarCharger.Model.Entities.TeslaSolarCharger.Car
        {
            Id = carId,
            Vin = $"{TestVin[..^1]}{carId}",
            CarType = CarType.Tesla,
            ShouldBeManaged = true,
            HomeDetectionVia = homeDetectionVia,
        });
        Context.SaveChangesAsync().GetAwaiter().GetResult();
        DetachAllEntities();
    }

    private void AddCarValueLogs(params TeslaSolarCharger.Model.Entities.TeslaSolarCharger.CarValueLog[] carValueLogs)
    {
        foreach (var carValueLog in carValueLogs)
        {
            Context.CarValueLogs.Add(carValueLog);
        }
        Context.SaveChangesAsync().GetAwaiter().GetResult();
        DetachAllEntities();
    }

    private static TeslaSolarCharger.Model.Entities.TeslaSolarCharger.CarValueLog CreateCarValueLog(int carId, CarValueType carValueType,
        DateTime timestamp, object value)
    {
        return new TeslaSolarCharger.Model.Entities.TeslaSolarCharger.CarValueLog
        {
            CarId = carId,
            Type = carValueType,
            Source = CarValueSource.FleetTelemetry,
            Timestamp = timestamp,
            DoubleValue = value as double?,
            IntValue = value as int?,
            BooleanValue = value as bool?,
        };
    }

    private async Task<List<DtoCar>> LoadCarsIntoSettings()
    {
        var settings = new Settings();
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ConfigJsonService>(
            new TypedParameter(typeof(ISettings), settings),
            new TypedParameter(typeof(ICarPropertyUpdateHelper), Mock.Create<CarPropertyUpdateHelper>()));
        await service.AddCarsToSettings(null);
        return settings.Cars;
    }

    private void SetupCarForBleDataCollectionTests(string? bleAdapterAddress = null)
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
            BleAdapterAddress = bleAdapterAddress,
        });
        Context.SaveChangesAsync().GetAwaiter().GetResult();
        DetachAllEntities();
        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar> { new() { Id = 1, Vin = TestVin, BleAdapterAddress = bleAdapterAddress } });
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
