using Autofac;
using Moq;
using PkSoftwareService.Custom.Backend.Ble;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos.Ble;
using TeslaSolarCharger.Server.Helper;
using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using Xunit;
using ChargingStateCase = CarServer.ChargeState.Types.ChargingState.TypeOneofCase;
using ClosureState = VCSEC.ClosureState_E;
using UserPresence = VCSEC.UserPresence_E;
using VehicleSleepStatus = VCSEC.VehicleSleepStatus_E;
using VehicleStatus = VCSEC.VehicleStatus;

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

    //Captured 2026-08-04 from a BLE container: awake, locked, unplugged, everything closed, nobody in the car. There
    //is no closureStatuses property because CLOSURESTATE_CLOSED is 0 in Tesla's VCSEC proto and protojson omits every
    //field at its proto3 default. Only a closure that is NOT closed is ever serialized.
    private const string AwakeBodyControllerStateJson =
        "{\"vehicleLockState\":\"VEHICLELOCKSTATE_LOCKED\",\"vehicleSleepStatus\":\"VEHICLE_SLEEP_STATUS_AWAKE\",\"userPresence\":\"VEHICLE_USER_PRESENCE_NOT_PRESENT\"}";

    //Captured 2026-08-04 from the same car with the driver door open and somebody at the wheel. The open door is the
    //only closure that survives serialization, and vehicleLockState is gone entirely because VEHICLELOCKSTATE_UNLOCKED
    //is 0 - the same omission rule, one field further along.
    private const string AwakeOpenDoorBodyControllerStateJson =
        "{\"closureStatuses\":{\"frontDriverDoor\":\"CLOSURESTATE_OPEN\"},\"vehicleSleepStatus\":\"VEHICLE_SLEEP_STATUS_AWAKE\",\"userPresence\":\"VEHICLE_USER_PRESENCE_PRESENT\"}";

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
        Assert.Equal(ChargingStateCase.Charging, chargeState.ChargingState.TypeCase);
    }

    [Fact]
    public void CanDeserializeDisconnectedChargeState()
    {
        const string json = "{\"chargeState\":{\"chargingState\":{\"Disconnected\":{}},\"batteryLevel\":62}}";
        var chargeState = TeslaSolarCharger.Server.Services.BleVehicleDataService.DeserializeChargeState(json);
        Assert.NotNull(chargeState);
        Assert.Equal(62, chargeState.BatteryLevel);
        Assert.Equal(ChargingStateCase.Disconnected, chargeState.ChargingState.TypeCase);
        Assert.False(TeslaSolarCharger.Server.Services.BleVehicleDataService.DerivePluggedIn(chargeState));
    }

    /// <summary>
    /// Tesla adds fields to these messages every few months. The parser is strict by default and would throw on the
    /// first unknown one, taking down BLE data collection for everyone until TSC is updated, so this must stay lenient.
    /// </summary>
    [Fact]
    public void UnknownFieldsDoNotBreakParsing()
    {
        const string json =
            "{\"chargeState\":{\"chargingState\":{\"Charging\":{}},\"batteryLevel\":62,\"someFieldTeslaAddedLater\":{\"nested\":123}}}";
        var chargeState = TeslaSolarCharger.Server.Services.BleVehicleDataService.DeserializeChargeState(json);
        Assert.NotNull(chargeState);
        Assert.Equal(62, chargeState.BatteryLevel);
        Assert.Equal(ChargingStateCase.Charging, chargeState.ChargingState.TypeCase);

        var bodyControllerState = TeslaSolarCharger.Server.Services.BleVehicleDataService.DeserializeBodyControllerState(
            "{\"vehicleSleepStatus\":\"VEHICLE_SLEEP_STATUS_AWAKE\",\"brandNewClosure\":\"CLOSURESTATE_OPEN\"}");
        Assert.NotNull(bodyControllerState);
        Assert.Equal(VehicleSleepStatus.VehicleSleepStatusAwake, bodyControllerState.VehicleSleepStatus);
    }

    /// <summary>
    /// A charge state without any charging state at all must read as "unknown", not as unplugged: acting on a wrong
    /// unplugged reading would stop a running charge.
    /// </summary>
    [Fact]
    public void MissingChargingStateIsUnknownRatherThanDisconnected()
    {
        var chargeState = TeslaSolarCharger.Server.Services.BleVehicleDataService.DeserializeChargeState(
            "{\"chargeState\":{\"batteryLevel\":62}}");
        Assert.NotNull(chargeState);
        Assert.Null(TeslaSolarCharger.Server.Services.BleVehicleDataService.DerivePluggedIn(chargeState));
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
        Assert.Equal(VehicleSleepStatus.VehicleSleepStatusAwake, awakeState.VehicleSleepStatus);
        //A closed up car carries no closure data at all, see the constant.
        Assert.Null(awakeState.ClosureStatuses);

        var openDoorState = TeslaSolarCharger.Server.Services.BleVehicleDataService.DeserializeBodyControllerState(AwakeOpenDoorBodyControllerStateJson);
        Assert.NotNull(openDoorState);
        Assert.Equal(ClosureState.ClosurestateOpen, openDoorState.ClosureStatuses!.FrontDriverDoor);
        //Every closure the car did not mention decodes to its proto3 default, which is CLOSURESTATE_CLOSED.
        Assert.Equal(ClosureState.ClosurestateClosed, openDoorState.ClosureStatuses.RearPassengerDoor);
        Assert.Equal(UserPresence.VehicleUserPresencePresent, openDoorState.UserPresence);

        var asleepState = TeslaSolarCharger.Server.Services.BleVehicleDataService.DeserializeBodyControllerState(AsleepBodyControllerStateJson);
        Assert.NotNull(asleepState);
        Assert.Equal(VehicleSleepStatus.VehicleSleepStatusAsleep, asleepState.VehicleSleepStatus);

        Assert.Null(TeslaSolarCharger.Server.Services.BleVehicleDataService.DeserializeBodyControllerState("no json"));
        Assert.Null(TeslaSolarCharger.Server.Services.BleVehicleDataService.DeserializeBodyControllerState(null));
    }

    [Fact]
    public void CanParseRealWorldChargeState()
    {
        //Real output of `tesla-control state charge` from a BLE container (2026-07-19), car plugged in but not
        //charging. Location values are replaced by dummy values.
        const string json = """
            {
              "chargeState": {
                "chargingState": {
                  "Stopped": {}
                },
                "fastChargerType": {
                  "ACSingleWireCAN": {}
                },
                "fastChargerBrand": {
                  "Tesla": {}
                },
                "chargeLimitSoc": 90,
                "chargeLimitSocStd": 80,
                "chargeLimitSocMin": 50,
                "chargeLimitSocMax": 100,
                "maxRangeChargeCounter": 0,
                "fastChargerPresent": false,
                "batteryRange": 172.81207,
                "idealBatteryRange": 172.81207,
                "batteryLevel": 47,
                "usableBatteryLevel": 47,
                "chargeEnergyAdded": 0.19999999,
                "chargeMilesAddedRated": 1,
                "chargeMilesAddedIdeal": 1,
                "chargerVoltage": 2,
                "chargerPilotCurrent": 16,
                "chargerActualCurrent": 0,
                "chargerPower": 0,
                "tripCharging": false,
                "chargeRateMph": 0,
                "chargePortDoorOpen": true,
                "connChargeCable": {
                  "IEC": {}
                },
                "scheduledChargingPending": false,
                "userChargeEnableRequest": false,
                "chargeEnableRequest": false,
                "chargerPhases": 2,
                "chargePortLatch": {
                  "Engaged": {}
                },
                "chargePortColdWeatherMode": false,
                "chargeCurrentRequest": 16,
                "chargeCurrentRequestMax": 16,
                "timestamp": "2026-07-19T19:13:18.497Z",
                "preconditioningTimes": {
                  "weekdays": {}
                },
                "offPeakChargingTimes": {},
                "scheduledChargingMode": "ScheduledChargingModeOff",
                "chargingAmps": 16,
                "preconditioningEnabled": false,
                "scheduledChargingStartTimeApp": -1,
                "superchargerSessionTripPlanner": false,
                "chargePortColor": "ChargePortColorOff",
                "chargeRateMphFloat": 0,
                "homeLocation": {
                  "latitude": 52.5185238,
                  "longitude": 13.3761736
                }
              }
            }
            """;
        var chargeState = TeslaSolarCharger.Server.Services.BleVehicleDataService.DeserializeChargeState(json);
        Assert.NotNull(chargeState);
        Assert.Equal(47, chargeState.BatteryLevel);
        Assert.Equal(90, chargeState.ChargeLimitSoc);
        Assert.Equal(2, chargeState.ChargerVoltage);
        Assert.Equal(0, chargeState.ChargerActualCurrent);
        //Note: the car reports 2 phases for 3 phase charging, DtoCar.ActualPhases converts this like on all other data sources.
        Assert.Equal(2, chargeState.ChargerPhases);
        Assert.Equal(16, chargeState.ChargeCurrentRequest);
        Assert.Equal(16, chargeState.ChargerPilotCurrent);
        Assert.Equal(ChargingStateCase.Stopped, chargeState.ChargingState.TypeCase);
        //0 A is a value the car actually reported, not a missing one - the distinction the generated types give us.
        Assert.True(chargeState.HasChargerActualCurrent);
        //Plugged in but not charging.
        Assert.True(TeslaSolarCharger.Server.Services.BleVehicleDataService.DerivePluggedIn(chargeState));
    }

    [Fact]
    public async Task BeaconMissDoesNotChangeStateBeforeAwayIsConfirmed()
    {
        var dtoCar = SetupBleDataCollectionCar();
        SetupBeaconScan(beaconFound: false);
        Mock.Mock<IBlePresenceStateService>().Setup(p => p.RegisterOutOfRange(dtoCar.Id, It.IsAny<DateTime>()))
            .Returns(BleAwayConfirmation.NotConfirmed);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        //A single miss can be a transient BLE failure of a car sitting in the garage: keep the last known state.
        Assert.Null(dtoCar.IsHomeGeofence.Value);
        Assert.Empty(Context.CarValueLogs.ToList());
        //An absent car must not be connected to at all, that is what makes an away car cheap.
        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(It.IsAny<string>()), Times.Never);
        Mock.Mock<IBleService>().Verify(b => b.GetChargeState(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmedAwayCarIsSetNotAtHomeOfflineAndChargingValuesAreReset()
    {
        var dtoCar = SetupBleDataCollectionCar();
        SetupBeaconScan(beaconFound: false);
        Mock.Mock<IBlePresenceStateService>().Setup(p => p.RegisterOutOfRange(dtoCar.Id, It.IsAny<DateTime>()))
            .Returns(BleAwayConfirmation.JustConfirmed);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Assert.False(dtoCar.IsHomeGeofence.Value);
        Assert.False(dtoCar.IsOnline.Value);
        var carValueLogs = Context.CarValueLogs.ToList();
        Assert.Contains(carValueLogs, l => l.Type == CarValueType.LocatedAtHome && l.BooleanValue == false && l.Source == CarValueSource.Ble);
        Assert.Contains(carValueLogs, l => l.Type == CarValueType.AsleepOrOffline && l.BooleanValue == true && l.Source == CarValueSource.Ble);
        //An away car can not be plugged in at home anymore. These values are inferred, not read from the car.
        Assert.Contains(carValueLogs, l => l.Type == CarValueType.IsPluggedIn && l.BooleanValue == false && l.Source == CarValueSource.Estimation);
        Assert.Contains(carValueLogs, l => l.Type == CarValueType.IsCharging && l.BooleanValue == false && l.Source == CarValueSource.Estimation);
        Assert.Contains(carValueLogs, l => l.Type == CarValueType.ChargeAmps && l.IntValue == 0 && l.Source == CarValueSource.Estimation);
    }

    [Fact]
    public async Task AlreadyConfirmedAwayCarDoesNotWriteValuesAgain()
    {
        var dtoCar = SetupBleDataCollectionCar();
        SetupBeaconScan(beaconFound: false);
        Mock.Mock<IBlePresenceStateService>().Setup(p => p.RegisterOutOfRange(dtoCar.Id, It.IsAny<DateTime>()))
            .Returns(BleAwayConfirmation.AlreadyConfirmed);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Assert.Empty(Context.CarValueLogs.ToList());
    }

    [Fact]
    public async Task ScanThatCouldNotRunDoesNotChangePresence()
    {
        var dtoCar = SetupBleDataCollectionCar();
        //A local problem (adapter unavailable, worker crashed, container unreachable) carries no information about
        //where the car is. Reporting such a failure as "not at home" was the defect this rework removes.
        Mock.Mock<IBleService>()
            .Setup(b => b.GetBeaconScanResults(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<List<string>>(), It.IsAny<int?>()))
            .ReturnsAsync(new DtoBleBeaconScanResult { Success = false, Outcome = BleCommandOutcome.AdapterUnavailable, ResultMessage = "hci0 is gone", });

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Assert.Null(dtoCar.IsHomeGeofence.Value);
        Assert.Empty(Context.CarValueLogs.ToList());
        Mock.Mock<IBlePresenceStateService>().Verify(p => p.RegisterOutOfRange(It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
        Mock.Mock<IErrorHandlingService>().Verify(e => e.HandleError(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task MissingConfiguredAdapterRaisesItsOwnIssueAndKeepsPresence()
    {
        var dtoCar = SetupBleDataCollectionCar();
        Mock.Mock<IBleService>()
            .Setup(b => b.GetBeaconScanResults(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<List<string>>(), It.IsAny<int?>()))
            .ReturnsAsync(new DtoBleBeaconScanResult { Success = false, Outcome = BleCommandOutcome.AdapterNotFound, });

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Assert.Null(dtoCar.IsHomeGeofence.Value);
        Mock.Mock<IBlePresenceStateService>().Verify(p => p.RegisterOutOfRange(It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
        Mock.Mock<IErrorHandlingService>().Verify(e => e.HandleError(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), Mock.Create<IIssueKeys>().BleAdapterNotFound, TestVin, It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task FoundBeaconResetsTheAwayCounterAndMarksCarAtHome()
    {
        var dtoCar = SetupBleDataCollectionCar();
        SetupBeaconScan(beaconFound: true);
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, ResultMessage = AsleepBodyControllerStateJson });

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Assert.True(dtoCar.IsHomeGeofence.Value);
        Mock.Mock<IBlePresenceStateService>().Verify(p => p.RegisterSuccessfulRead(dtoCar.Id), Times.Once);
        Mock.Mock<IBlePresenceStateService>().Verify(p => p.RegisterOutOfRange(It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task InfotainmentIsNotPolledWhileTheCarIsInASleepWindow()
    {
        var dtoCar = SetupBleDataCollectionCar();
        SetupBeaconScan(beaconFound: true);
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, ResultMessage = AwakeBodyControllerStateJson });
        //The car is inside a sleep window: the infotainment poll is what keeps it awake, so it has to be withheld.
        Mock.Mock<IBleSleepWindowService>()
            .Setup(s => s.ShouldPollInfotainment(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<int>()))
            .Returns(false);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Mock.Mock<IBleService>().Verify(b => b.GetChargeState(It.IsAny<string>()), Times.Never);
        //Presence and online state still come from the beacon scan and the VCSEC read, neither of which wakes the car.
        Assert.True(dtoCar.IsHomeGeofence.Value);
        Assert.True(dtoCar.IsOnline.Value);
    }

    [Fact]
    public async Task SleepingCarIsReportedToTheSleepWindowService()
    {
        var dtoCar = SetupBleDataCollectionCar();
        SetupBeaconScan(beaconFound: true);
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, ResultMessage = AsleepBodyControllerStateJson });

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        //The sleep attempt succeeded, which the UI shows as the asleep phase.
        Mock.Mock<IBleSleepWindowService>().Verify(s => s.NotifyAsleep(dtoCar.Id), Times.Once);
    }

    [Fact]
    public async Task CarIsNotReadWhileAnotherReadForItIsInProgress()
    {
        var dtoCar = SetupBleDataCollectionCar();
        SetupBeaconScan(beaconFound: true);
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, ResultMessage = AsleepBodyControllerStateJson });
        //Another read for this car is already running, e.g. an on demand single car read overlapping the scheduled job.
        Mock.Mock<IBleReadCoordinator>().Setup(c => c.TryBeginRead(dtoCar.Id)).Returns(false);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        //The car must be left entirely alone: no reads, no state writes and no released slot it never acquired.
        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(It.IsAny<string>()), Times.Never);
        Mock.Mock<IBlePresenceStateService>().Verify(p => p.RegisterSuccessfulRead(It.IsAny<int>()), Times.Never);
        Mock.Mock<IBleReadCoordinator>().Verify(c => c.EndRead(It.IsAny<int>()), Times.Never);
        Assert.Null(dtoCar.IsHomeGeofence.Value);
    }

    [Fact]
    public async Task FailedReadAfterBeaconHitKeepsCarAtHome()
    {
        var dtoCar = SetupBleDataCollectionCar();
        SetupBeaconScan(beaconFound: true);
        //The car provably advertises, so a failed connect right afterwards is radio or car trouble, never absence.
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult
            {
                Success = false,
                Outcome = BleCommandOutcome.LinkFailed,
                BeaconFound = true,
                ResultMessage = "failed to connect: timed out",
            });

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Assert.True(dtoCar.IsHomeGeofence.Value);
        Mock.Mock<IBlePresenceStateService>().Verify(p => p.RegisterOutOfRange(It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
        //The radio problem must surface as an error instead of being silently resolved as "car left".
        Mock.Mock<IErrorHandlingService>().Verify(e => e.HandleError(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), Mock.Create<IIssueKeys>().BleDataCollectionError, TestVin, It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task AsleepCarIsAtHomeButChargeStateIsNotPolled()
    {
        var dtoCar = SetupBleDataCollectionCar();
        SetupBeaconScan(beaconFound: true);
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, ResultMessage = AsleepBodyControllerStateJson });

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
        SetupBeaconScan(beaconFound: true);
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, ResultMessage = AwakeBodyControllerStateJson });
        Mock.Mock<IBleService>().Setup(b => b.GetChargeState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, ResultMessage = ChargingChargeStateJson });

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
    public async Task ChargingCarNeverEntersASleepWindow()
    {
        var dtoCar = SetupBleDataCollectionCar();
        dtoCar.IsCharging.Update(new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero), true);
        SetupBeaconScan(beaconFound: true);
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, ResultMessage = AwakeBodyControllerStateJson });
        Mock.Mock<IBleService>().Setup(b => b.GetChargeState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, ResultMessage = ChargingChargeStateJson });

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        //Nothing in the tracked signature changes while a car charges steadily, so feeding the poll into the state
        //machine would silence the car after the stability period. The window state is cleared instead.
        Mock.Mock<IBleSleepWindowService>().Verify(s => s.ResetSleepWindow(dtoCar.Id), Times.Once);
        Mock.Mock<IBleSleepWindowService>().Verify(s => s.ObserveFullPoll(It.IsAny<int>(), It.IsAny<VehicleStatus>(),
            It.IsAny<bool?>(), It.IsAny<int?>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        //The charge state is read every cycle regardless, so TSC never goes blind while the car charges.
        Mock.Mock<IBleService>().Verify(b => b.GetChargeState(TestVin), Times.Once);
    }

    [Fact]
    public async Task RefreshSingleCarDataUpdatesOnlyThatCar()
    {
        var dtoCar = SetupBleDataCollectionCar();
        SetupBeaconScan(beaconFound: true);
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, ResultMessage = AsleepBodyControllerStateJson });

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshSingleCarData(dtoCar.Id);

        Assert.True(dtoCar.IsHomeGeofence.Value);
        Mock.Mock<IBlePresenceStateService>().Verify(p => p.RegisterSuccessfulRead(dtoCar.Id), Times.Once);
        //A single car read must not touch the container's warm window, that belongs to the scheduled poll alone.
        Mock.Mock<IBleService>().Verify(b => b.GetBeaconScanResults(It.IsAny<string?>(), It.IsAny<string?>(),
            It.Is<List<string>>(v => v.Contains(TestVin)), null), Times.Once);
    }

    [Fact]
    public async Task RefreshSingleCarDataDoesNothingForNonBleCars()
    {
        var dtoCar = SetupBleDataCollectionCar(useFleetTelemetry: true);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshSingleCarData(dtoCar.Id);

        Mock.Mock<IBleService>().Verify(b => b.GetBeaconScanResults(It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<List<string>>(), It.IsAny<int?>()), Times.Never);
        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ScheduledPollKeepsTheWorkerWarm()
    {
        SetupBleDataCollectionCar();
        SetupBeaconScan(beaconFound: false);
        Mock.Mock<IBlePresenceStateService>().Setup(p => p.RegisterOutOfRange(It.IsAny<int>(), It.IsAny<DateTime>()))
            .Returns(BleAwayConfirmation.NotConfirmed);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        //Only the scheduled poll sends keepWarm, so the worker survives between polls without a one off command ever
        //changing the container's warm window.
        Mock.Mock<IBleService>().Verify(b => b.GetBeaconScanResults(It.IsAny<string?>(), It.IsAny<string?>(),
            It.Is<List<string>>(v => v.Contains(TestVin)), BleConstants.BleKeepWarmSeconds), Times.Once);
    }

    [Fact]
    public async Task DoesNotPollWhenGetVehicleDataViaBleIsDisabled()
    {
        SetupBleDataCollectionCar();
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.GetVehicleDataViaBle()).Returns(false);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Mock.Mock<IBleService>().Verify(b => b.GetBeaconScanResults(It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<List<string>>(), It.IsAny<int?>()), Times.Never);
        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DoesNotPollCarsStillUsingFleetTelemetry()
    {
        SetupBleDataCollectionCar(useFleetTelemetry: true);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Mock.Mock<IBleService>().Verify(b => b.GetBeaconScanResults(It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<List<string>>(), It.IsAny<int?>()), Times.Never);
        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(It.IsAny<string>()), Times.Never);
    }

    private void SetupBeaconScan(bool beaconFound)
    {
        Mock.Mock<IBleService>()
            .Setup(b => b.GetBeaconScanResults(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<List<string>>(), It.IsAny<int?>()))
            .ReturnsAsync(new DtoBleBeaconScanResult
            {
                Success = true,
                Outcome = BleCommandOutcome.Ok,
                WindowMs = 3000,
                ScanDurationMs = beaconFound ? 48 : 3000,
                //Other advertisements prove the radio receives; they no longer influence presence but are kept as
                //diagnostics for the radio silence warning.
                OtherAdvertisementsSeen = 12,
                DistinctDevicesSeen = 4,
                Vehicles = new List<DtoBleBeaconVehicleResult>
                {
                    new() { Vin = TestVin, BeaconFound = beaconFound, Rssi = beaconFound ? -63 : null, },
                },
            });
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
        //No concurrent read in these tests, so the coordinator always grants the read slot. Without this the
        //auto mocked coordinator returns false and every refresh would silently do nothing.
        Mock.Mock<IBleReadCoordinator>().Setup(c => c.TryBeginRead(It.IsAny<int>())).Returns(true);
        //The sleep window is covered by BleSleepWindowServiceTests; here no car is ever inside a window, so the
        //infotainment poll always happens (the auto mocked service would otherwise withhold it by returning false).
        Mock.Mock<IBleSleepWindowService>()
            .Setup(s => s.ShouldPollInfotainment(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<int>()))
            .Returns(true);
        return dtoCar;
    }
}
