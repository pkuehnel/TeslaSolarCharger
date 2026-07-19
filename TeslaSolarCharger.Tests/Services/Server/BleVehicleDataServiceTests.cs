using Autofac;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Helper;
using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Ble;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class BleVehicleDataServiceTests : TestBase
{
    private const string TestVin = "TESTVIN123456789A";

    //Realistic protojson output of `tesla-control state charge`: the charge state is wrapped in a VehicleData message
    //and the charging state is an object with a single property as it is a protobuf oneof of empty messages.
    private const string ChargingChargeStateJson = """
        {
          "chargeState": {
            "chargingState": { "Charging": {} },
            "batteryLevel": 55,
            "chargeLimitSoc": 80,
            "chargerVoltage": 231,
            "chargerActualCurrent": 16,
            "chargerPhases": 3,
            "chargeCurrentRequest": 16,
            "chargerPilotCurrent": 16,
            "minutesToFullCharge": 90
          }
        }
        """;

    private const string AwakeBodyControllerStateJson =
        "{\"closureStatuses\":{\"frontDriverDoor\":\"CLOSURESTATE_CLOSED\"},\"vehicleLockState\":\"VEHICLELOCKSTATE_UNLOCKED\",\"vehicleSleepStatus\":\"VEHICLE_SLEEP_STATUS_AWAKE\",\"userPresence\":\"VEHICLE_USER_PRESENCE_NOT_PRESENT\"}";

    private const string AsleepBodyControllerStateJson =
        "{\"vehicleLockState\":\"VEHICLELOCKSTATE_LOCKED\",\"vehicleSleepStatus\":\"VEHICLE_SLEEP_STATUS_ASLEEP\",\"userPresence\":\"VEHICLE_USER_PRESENCE_UNKNOWN\"}";

    public BleVehicleDataServiceTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public void CanDeserializeChargeState()
    {
        var chargeState = TeslaSolarCharger.Server.Services.BleVehicleDataService.DeserializeChargeState(ChargingChargeStateJson);
        Assert.NotNull(chargeState);
        Assert.Equal(55, chargeState.BatteryLevel);
        Assert.Equal(80, chargeState.ChargeLimitSoc);
        Assert.Equal(231, chargeState.ChargerVoltage);
        Assert.Equal(16, chargeState.ChargerActualCurrent);
        Assert.Equal(3, chargeState.ChargerPhases);
        Assert.Equal(16, chargeState.ChargeCurrentRequest);
        Assert.Equal(16, chargeState.ChargerPilotCurrent);
        Assert.Equal(90, chargeState.MinutesToFullCharge);
        Assert.Equal("Charging", TeslaSolarCharger.Server.Services.BleVehicleDataService.GetChargingStateName(chargeState.ChargingState));
    }

    [Fact]
    public void CanDeserializeDisconnectedChargeState()
    {
        const string json = "{\"chargeState\":{\"chargingState\":{\"Disconnected\":{}},\"batteryLevel\":62}}";
        var chargeState = TeslaSolarCharger.Server.Services.BleVehicleDataService.DeserializeChargeState(json);
        Assert.NotNull(chargeState);
        Assert.Equal(62, chargeState.BatteryLevel);
        Assert.Equal("Disconnected", TeslaSolarCharger.Server.Services.BleVehicleDataService.GetChargingStateName(chargeState.ChargingState));
    }

    [Fact]
    public void ChargingStateNameSupportsStringSerialization()
    {
        Assert.Equal("Charging", TeslaSolarCharger.Server.Services.BleVehicleDataService.GetChargingStateName(new JValue("Charging")));
        Assert.Null(TeslaSolarCharger.Server.Services.BleVehicleDataService.GetChargingStateName(null));
        Assert.Null(TeslaSolarCharger.Server.Services.BleVehicleDataService.GetChargingStateName(new JObject()));
    }

    [Fact]
    public void ReturnsNullOnInvalidChargeStateJson()
    {
        Assert.Null(TeslaSolarCharger.Server.Services.BleVehicleDataService.DeserializeChargeState("Failed to execute command"));
        Assert.Null(TeslaSolarCharger.Server.Services.BleVehicleDataService.DeserializeChargeState(null));
        Assert.Null(TeslaSolarCharger.Server.Services.BleVehicleDataService.DeserializeChargeState(""));
    }

    [Fact]
    public void CanDeserializeBodyControllerState()
    {
        var awakeState = TeslaSolarCharger.Server.Services.BleVehicleDataService.DeserializeBodyControllerState(AwakeBodyControllerStateJson);
        Assert.NotNull(awakeState);
        Assert.Equal("VEHICLE_SLEEP_STATUS_AWAKE", awakeState.VehicleSleepStatus);

        var asleepState = TeslaSolarCharger.Server.Services.BleVehicleDataService.DeserializeBodyControllerState(AsleepBodyControllerStateJson);
        Assert.NotNull(asleepState);
        Assert.Equal("VEHICLE_SLEEP_STATUS_ASLEEP", asleepState.VehicleSleepStatus);

        Assert.Null(TeslaSolarCharger.Server.Services.BleVehicleDataService.DeserializeBodyControllerState("no json"));
        Assert.Null(TeslaSolarCharger.Server.Services.BleVehicleDataService.DeserializeBodyControllerState(null));
    }

    [Fact]
    public void DetectsBeaconNotFoundResults()
    {
        Assert.True(TeslaSolarCharger.Server.Services.BleVehicleDataService.IsBeaconNotFoundResult(new DtoBleCommandResult
        {
            Success = false,
            ResultMessage = $"Error: failed to find BLE beacon for {TestVin} (S1a87a5a75f3df858C)",
        }));
        Assert.False(TeslaSolarCharger.Server.Services.BleVehicleDataService.IsBeaconNotFoundResult(new DtoBleCommandResult
        {
            Success = false,
            ResultMessage = "Error: failed to connect to vehicle: context deadline exceeded",
        }));
        Assert.False(TeslaSolarCharger.Server.Services.BleVehicleDataService.IsBeaconNotFoundResult(new DtoBleCommandResult
        {
            Success = false,
            ResultMessage = null,
        }));
    }

    [Fact]
    public async Task BeaconNotFoundSetsCarNotAtHomeAndOffline()
    {
        var dtoCar = SetupBleDataCollectionCar();
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = false, ResultMessage = $"Error: failed to find BLE beacon for {TestVin}" });

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Assert.False(dtoCar.IsHomeGeofence.Value);
        Assert.False(dtoCar.IsOnline.Value);
        var carValueLogs = Context.CarValueLogs.ToList();
        Assert.Contains(carValueLogs, l => l.Type == CarValueType.LocatedAtHome && l.BooleanValue == false && l.Source == CarValueSource.Ble);
        Assert.Contains(carValueLogs, l => l.Type == CarValueType.AsleepOrOffline && l.BooleanValue == true && l.Source == CarValueSource.Ble);
        Mock.Mock<IBleService>().Verify(b => b.GetChargeState(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AsleepCarIsAtHomeButChargeStateIsNotPolled()
    {
        var dtoCar = SetupBleDataCollectionCar();
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = true, ResultMessage = AsleepBodyControllerStateJson });

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Assert.True(dtoCar.IsHomeGeofence.Value);
        Assert.False(dtoCar.IsOnline.Value);
        Mock.Mock<IBleService>().Verify(b => b.GetChargeState(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AwakeCarGetsChargeStateValues()
    {
        var dtoCar = SetupBleDataCollectionCar();
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = true, ResultMessage = AwakeBodyControllerStateJson });
        Mock.Mock<IBleService>().Setup(b => b.GetChargeState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = true, ResultMessage = ChargingChargeStateJson });

        //Use the real property update helper so the DtoCar properties are actually updated.
        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>(
            new TypedParameter(typeof(ICarPropertyUpdateHelper), Mock.Create<CarPropertyUpdateHelper>()));
        await service.RefreshBleCarData();

        Assert.True(dtoCar.IsHomeGeofence.Value);
        Assert.True(dtoCar.IsOnline.Value);
        Assert.Equal(55, dtoCar.SoC.Value);
        Assert.Equal(80, dtoCar.SocLimit.Value);
        Assert.Equal(231, dtoCar.ChargerVoltage.Value);
        Assert.Equal(16, dtoCar.ChargerActualCurrent.Value);
        Assert.Equal(3, dtoCar.ChargerPhases.Value);
        Assert.Equal(16, dtoCar.ChargerRequestedCurrent.Value);
        Assert.Equal(16, dtoCar.ChargerPilotCurrent.Value);
        Assert.True(dtoCar.PluggedIn.Value);
        Assert.True(dtoCar.IsCharging.Value);
        var carValueLogs = Context.CarValueLogs.ToList();
        Assert.Contains(carValueLogs, l => l.Type == CarValueType.StateOfCharge && l.IntValue == 55 && l.Source == CarValueSource.Ble);
        Assert.Contains(carValueLogs, l => l.Type == CarValueType.IsCharging && l.BooleanValue == true && l.Source == CarValueSource.Ble);
    }

    [Fact]
    public async Task DoesNotPollWhenGetVehicleDataViaBleIsDisabled()
    {
        SetupBleDataCollectionCar();
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.GetVehicleDataViaBle()).Returns(false);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DoesNotPollCarsStillUsingFleetTelemetry()
    {
        SetupBleDataCollectionCar(useFleetTelemetry: true);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(It.IsAny<string>()), Times.Never);
    }

    private DtoCar SetupBleDataCollectionCar(bool useFleetTelemetry = false)
    {
        Context.Cars.Add(new Car
        {
            Id = 1,
            Vin = TestVin,
            CarType = CarType.Tesla,
            ShouldBeManaged = true,
            UseBle = true,
            UseFleetTelemetry = useFleetTelemetry,
            IncludeTrackingRelevantFields = false,
        });
        Context.SaveChangesAsync().GetAwaiter().GetResult();
        DetachAllEntities();

        var dtoCar = new DtoCar
        {
            Id = 1,
            Vin = TestVin,
        };
        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar> { dtoCar });
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.GetVehicleDataViaBle()).Returns(true);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.GetVehicleDataFromTesla()).Returns(true);
        return dtoCar;
    }
}
