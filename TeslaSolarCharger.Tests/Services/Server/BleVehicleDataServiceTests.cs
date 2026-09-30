using Autofac;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
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
        SetupPresence(present: false);
        Mock.Mock<IBlePresenceStateService>().Setup(p => p.RegisterPresenceAge(dtoCar.Id, It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan>()))
            .Returns(BlePresenceDecision.Uncertain);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        //A single miss can be a transient BLE failure of a car sitting in the garage: keep the last known state.
        Assert.Null(dtoCar.IsHomeGeofence.Value);
        Assert.Empty(Context.CarValueLogs.ToList());
        //An absent car must not be connected to at all, that is what makes an away car cheap.
        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(It.IsAny<string>()), Times.Never);
        Mock.Mock<IBleService>().Verify(b => b.GetChargeState(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// The poll used to connect to a car that rejects TSC's key every few seconds forever, which filled the radio
    /// and the error list while the one thing that would fix it - adding the key - was never named.
    /// </summary>
    [Fact]
    public async Task CarThatRejectsTheKeyIsNotAskedAndTheUserIsToldToAddIt()
    {
        var dtoCar = SetupBleDataCollectionCar();
        SetupPresence(present: true);
        Mock.Mock<IBleAccessGateService>().Setup(g => g.IsKeyRejected(dtoCar.Id)).Returns(true);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(It.IsAny<string>()), Times.Never);
        Mock.Mock<IBleService>().Verify(b => b.GetChargeState(It.IsAny<string>()), Times.Never);
        //The beacon still proves the car is at home, that costs no radio time.
        Assert.True(dtoCar.IsHomeGeofence.Value);
        Mock.Mock<IErrorHandlingService>().Verify(e => e.HandleError(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.Is<string>(m => m.Contains("key", StringComparison.OrdinalIgnoreCase)),
            Mock.Create<IIssueKeys>().BleDataCollectionError, TestVin, It.IsAny<string?>()), Times.Once);
    }

    /// <summary>
    /// Adding a key takes the container's adapter for itself. A read sent meanwhile can only time out, and it takes
    /// the radio away from the pairing the user is standing at their car for.
    /// </summary>
    [Fact]
    public async Task CarIsNotReadWhileAKeyIsBeingAddedOnItsContainer()
    {
        var dtoCar = SetupBleDataCollectionCar();
        SetupPresence(present: true);
        Mock.Mock<IBleAccessGateService>().Setup(g => g.IsPairingInProgress(It.IsAny<string?>())).Returns(true);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(It.IsAny<string>()), Times.Never);
        Mock.Mock<IBleService>().Verify(b => b.GetChargeState(It.IsAny<string>()), Times.Never);
        //Nothing is wrong here, so the pause must not look like a problem in the error list.
        Mock.Mock<IErrorHandlingService>().Verify(e => e.HandleError(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmedAwayCarIsSetNotAtHomeOfflineAndChargingValuesAreReset()
    {
        var dtoCar = SetupBleDataCollectionCar();
        SetupPresence(present: false);
        Mock.Mock<IBlePresenceStateService>().Setup(p => p.RegisterPresenceAge(dtoCar.Id, It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan>()))
            .Returns(BlePresenceDecision.JustConfirmedAway);

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
        SetupPresence(present: false);
        Mock.Mock<IBlePresenceStateService>().Setup(p => p.RegisterPresenceAge(dtoCar.Id, It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan>()))
            .Returns(BlePresenceDecision.AlreadyAway);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Assert.Empty(Context.CarValueLogs.ToList());
    }

    /// <summary>
    /// A presence request that failed while the car was already gone used to leave an error open until the car came
    /// back and was read successfully, which can be days: only the one shot away transition resolved it.
    /// </summary>
    [Theory]
    [InlineData(BlePresenceDecision.JustConfirmedAway)]
    [InlineData(BlePresenceDecision.AlreadyAway)]
    [InlineData(BlePresenceDecision.Uncertain)]
    [InlineData(BlePresenceDecision.Unknown)]
    public async Task ACarThatIsNotReadHasNoOpenDataCollectionError(BlePresenceDecision decision)
    {
        var dtoCar = SetupBleDataCollectionCar();
        SetupPresence(present: false);
        Mock.Mock<IBlePresenceStateService>().Setup(p => p.RegisterPresenceAge(dtoCar.Id, It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan>()))
            .Returns(decision);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        //The container answered and the car was not talked to at all, so nothing can currently be failing.
        Mock.Mock<IErrorHandlingService>().Verify(e => e.HandleErrorResolved(
            Mock.Create<IIssueKeys>().BleDataCollectionError, TestVin), Times.Once);
    }

    [Fact]
    public async Task ScanThatCouldNotRunDoesNotChangePresence()
    {
        var dtoCar = SetupBleDataCollectionCar();
        //A local problem (adapter unavailable, worker crashed, container unreachable) carries no information about
        //where the car is. Reporting such a failure as "not at home" was the defect this rework removes.
        Mock.Mock<IBleService>()
            .Setup(b => b.GetPresence(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<List<string>>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new DtoBlePresenceResult { ErrorMessage = "hci0 is gone", });

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Assert.Null(dtoCar.IsHomeGeofence.Value);
        Assert.Empty(Context.CarValueLogs.ToList());
        Mock.Mock<IBlePresenceStateService>().Verify(p => p.RegisterPresenceAge(It.IsAny<int>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan>()), Times.Never);
        Mock.Mock<IErrorHandlingService>().Verify(e => e.HandleError(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task MissingConfiguredAdapterRaisesItsOwnIssueAndKeepsPresence()
    {
        var dtoCar = SetupBleDataCollectionCar();
        Mock.Mock<IBleService>()
            .Setup(b => b.GetPresence(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<List<string>>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new DtoBlePresenceResult { ErrorMessage = "The configured Bluetooth adapter hci9 is not present on this host.", });

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Assert.Null(dtoCar.IsHomeGeofence.Value);
        Mock.Mock<IBlePresenceStateService>().Verify(p => p.RegisterPresenceAge(It.IsAny<int>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan>()), Times.Never);
        Mock.Mock<IErrorHandlingService>().Verify(e => e.HandleError(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), Mock.Create<IIssueKeys>().BleAdapterNotFound, TestVin, It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task AFreshlyHeardCarIsMarkedAtHome()
    {
        var dtoCar = SetupBleDataCollectionCar();
        SetupPresence(present: true);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.BlePresenceMaxAgeSeconds()).Returns(90);
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, ResultMessage = AsleepBodyControllerStateJson });

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Assert.True(dtoCar.IsHomeGeofence.Value);
        //The age the container reported has to be what the decision is made on, not a rounded or defaulted value.
        Mock.Mock<IBlePresenceStateService>().Verify(p => p.RegisterPresenceAge(dtoCar.Id,
            TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(90)), Times.Once);
    }

    [Fact]
    public async Task InfotainmentIsNotPolledWhileTheCarIsInASleepWindow()
    {
        var dtoCar = SetupBleDataCollectionCar();
        SetupPresence(present: true);
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
        SetupPresence(present: true);
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
        SetupPresence(present: true);
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, ResultMessage = AsleepBodyControllerStateJson });
        //Another read for this car is already running, e.g. an on demand single car read overlapping the scheduled job.
        Mock.Mock<IBleReadCoordinator>().Setup(c => c.TryBeginRead(dtoCar.Id)).Returns(false);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        //The car must be left entirely alone: no reads, no state writes and no released slot it never acquired.
        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(It.IsAny<string>()), Times.Never);
        Mock.Mock<IBlePresenceStateService>().Verify(p => p.RegisterPresenceAge(It.IsAny<int>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan>()), Times.Never);
        Mock.Mock<IBleReadCoordinator>().Verify(c => c.EndRead(It.IsAny<int>()), Times.Never);
        Assert.Null(dtoCar.IsHomeGeofence.Value);
    }

    [Fact]
    public async Task FailedReadOfAPresentCarKeepsItAtHome()
    {
        var dtoCar = SetupBleDataCollectionCar();
        SetupPresence(present: true);
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
        //The radio problem must surface as an error instead of being silently resolved as "car left".
        Mock.Mock<IErrorHandlingService>().Verify(e => e.HandleError(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), Mock.Create<IIssueKeys>().BleDataCollectionError, TestVin, It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task AsleepCarIsAtHomeButChargeStateIsNotPolled()
    {
        var dtoCar = SetupBleDataCollectionCar();
        SetupPresence(present: true);
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
        SetupPresence(present: true);
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
        SetupPresence(present: true);
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
        SetupPresence(present: true);
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, ResultMessage = AsleepBodyControllerStateJson });

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshSingleCarData(dtoCar.Id);

        Assert.True(dtoCar.IsHomeGeofence.Value);
        Mock.Mock<IBlePresenceStateService>().Verify(p => p.RegisterPresenceAge(dtoCar.Id, It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan>()), Times.Once);
        //A single car read must not touch the container's warm window, that belongs to the scheduled poll alone.
        Mock.Mock<IBleService>().Verify(b => b.GetPresence(It.IsAny<string?>(), It.IsAny<string?>(),
            It.Is<List<string>>(v => v.Contains(TestVin)), null, It.IsAny<int?>()), Times.Once);
    }

    [Fact]
    public async Task RefreshSingleCarDataDoesNothingForNonBleCars()
    {
        var dtoCar = SetupBleDataCollectionCar(useFleetTelemetry: true);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshSingleCarData(dtoCar.Id);

        Mock.Mock<IBleService>().Verify(b => b.GetPresence(It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<List<string>>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ScheduledPollKeepsTheWorkerWarm()
    {
        SetupBleDataCollectionCar();
        SetupPresence(present: false);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        //Only the scheduled poll sends keepWarm, so the worker survives between polls without a one off command ever
        //changing the container's warm window.
        Mock.Mock<IBleService>().Verify(b => b.GetPresence(It.IsAny<string?>(), It.IsAny<string?>(),
            It.Is<List<string>>(v => v.Contains(TestVin)), BleConstants.BleKeepWarmSeconds, It.IsAny<int?>()), Times.Once);
    }

    /// <summary>
    /// The max age decides whether a car counts as being at home, so the configured value has to reach the container
    /// rather than leaving it on its own default.
    /// </summary>
    [Fact]
    public async Task ScheduledPollSendsTheConfiguredPresenceMaxAge()
    {
        SetupBleDataCollectionCar();
        SetupPresence(present: false);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.BlePresenceMaxAgeSeconds()).Returns(11);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Mock.Mock<IBleService>().Verify(b => b.GetPresence(It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<List<string>>(), It.IsAny<int?>(), 11), Times.Once);
    }

    [Fact]
    public async Task DoesNotPollWhenGetVehicleDataViaBleIsDisabled()
    {
        SetupBleDataCollectionCar();
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.GetVehicleDataViaBle()).Returns(false);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Mock.Mock<IBleService>().Verify(b => b.GetPresence(It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<List<string>>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DoesNotPollCarsStillUsingFleetTelemetry()
    {
        SetupBleDataCollectionCar(useFleetTelemetry: true);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Mock.Mock<IBleService>().Verify(b => b.GetPresence(It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<List<string>>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// The container answers how long ago the car was last heard. A present car is a few hundred milliseconds old,
    /// an absent one far past any max age; the presence decision itself is the state service.s job and mocked
    /// separately.
    /// </summary>
    private void SetupPresence(bool present)
    {
        Mock.Mock<IBleService>()
            .Setup(b => b.GetPresence(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<List<string>>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new DtoBlePresenceResult
            {
                ScannerRunning = true,
                WarmingUp = false,
                MaxAgeMs = 90000,
                //Radio evidence: it no longer influences presence but still drives the radio silence warning.
                AdvertisementsSeen = 4711,
                LastAdvertisementMsAgo = 120,
                Vehicles = new List<DtoBlePresenceVehicle>
                {
                    new()
                    {
                        Vin = TestVin,
                        Heard = present,
                        LastSeenMsAgo = present ? 300 : 600000,
                        Rssi = present ? -63 : null,
                        LastSource = present ? "advertisement" : null,
                    },
                },
            });
        Mock.Mock<IBlePresenceStateService>()
            .Setup(p => p.RegisterPresenceAge(It.IsAny<int>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan>()))
            .Returns(present ? BlePresenceDecision.Present : BlePresenceDecision.Uncertain);
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

    /// <summary>
    /// The regression that started this whole rework: a car that is not around must cost nothing at all. The old
    /// design paid a scan window for it, and asking the car directly would pay a full connect timeout.
    /// </summary>
    [Fact]
    public async Task AnAbsentCarIsNeverTalkedTo()
    {
        SetupBleDataCollectionCar();
        SetupPresence(present: false);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(It.IsAny<string>()), Times.Never);
        Mock.Mock<IBleService>().Verify(b => b.GetChargeState(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// Right after a container or worker restart the scan has heard nothing yet, so every car reads as not present.
    /// Concluding anything from that would mark every car away on every restart.
    /// </summary>
    [Fact]
    public async Task AWarmingUpScannerConcludesNothingAboutAnyCar()
    {
        var dtoCar = SetupBleDataCollectionCar();
        Mock.Mock<IBleService>()
            .Setup(b => b.GetPresence(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<List<string>>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new DtoBlePresenceResult
            {
                ScannerRunning = true,
                WarmingUp = true,
                MaxAgeMs = 90000,
                Vehicles = new List<DtoBlePresenceVehicle>
                {
                    new() { Vin = TestVin, Heard = false, LastSeenMsAgo = null, },
                },
            });

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        //Null age means "nothing is known", which the presence service turns into Unknown rather than a miss.
        Mock.Mock<IBlePresenceStateService>().Verify(p => p.RegisterPresenceAge(dtoCar.Id, null, It.IsAny<TimeSpan>()), Times.Once);
        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(It.IsAny<string>()), Times.Never);
        Assert.Null(dtoCar.IsHomeGeofence.Value);
    }

    /// <summary>
    /// The field regression this test exists for: the deaf adapter watchdog restarted the worker every few minutes,
    /// and each restart made the container report a warm up for a full max age. Reading the flag before the evidence
    /// threw away advertisements that were milliseconds old, so a car sitting in the driveway was declared unknown
    /// and left untouched for about ninety seconds every five minutes.
    ///
    /// Warming up means "do not conclude absence", never "ignore a car we can hear right now".
    /// </summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task FreshEvidenceProvesPresenceWhateverTheScannerReportsAboutItself(bool warmingUp, bool scannerRunning)
    {
        var dtoCar = SetupBleDataCollectionCar();
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.BlePresenceMaxAgeSeconds()).Returns(90);
        Mock.Mock<IBleService>()
            .Setup(b => b.GetPresence(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<List<string>>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new DtoBlePresenceResult
            {
                ScannerRunning = scannerRunning,
                WarmingUp = warmingUp,
                MaxAgeMs = 90000,
                LastAdvertisementMsAgo = 4,
                Vehicles = new List<DtoBlePresenceVehicle>
                {
                    new() { Vin = TestVin, Heard = true, LastSeenMsAgo = 4, LastSource = "advertisement", Rssi = -65, },
                },
            });
        Mock.Mock<IBlePresenceStateService>()
            .Setup(p => p.RegisterPresenceAge(It.IsAny<int>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan>()))
            .Returns(BlePresenceDecision.Present);
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, ResultMessage = AsleepBodyControllerStateJson });

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Mock.Mock<IBlePresenceStateService>().Verify(
            p => p.RegisterPresenceAge(dtoCar.Id, TimeSpan.FromMilliseconds(4), It.IsAny<TimeSpan>()), Times.Once);
        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(TestVin), Times.Once);
    }

    /// <summary>
    /// The other half of the same rule: once the evidence is older than the max age the flags do matter again,
    /// because then the answer really is ignorance rather than absence.
    /// </summary>
    [Fact]
    public async Task StaleEvidenceConcludesNothingWhileTheScannerIsStillWarmingUp()
    {
        var dtoCar = SetupBleDataCollectionCar();
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.BlePresenceMaxAgeSeconds()).Returns(90);
        Mock.Mock<IBleService>()
            .Setup(b => b.GetPresence(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<List<string>>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new DtoBlePresenceResult
            {
                ScannerRunning = true,
                WarmingUp = true,
                MaxAgeMs = 90000,
                Vehicles = new List<DtoBlePresenceVehicle>
                {
                    new() { Vin = TestVin, Heard = false, LastSeenMsAgo = 600000, },
                },
            });

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Mock.Mock<IBlePresenceStateService>().Verify(p => p.RegisterPresenceAge(dtoCar.Id, null, It.IsAny<TimeSpan>()), Times.Once);
        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// A stopped scan is not a silent car either: presence answers carry no information while nothing is listening.
    /// </summary>
    [Fact]
    public async Task AStoppedScannerConcludesNothingAboutAnyCar()
    {
        var dtoCar = SetupBleDataCollectionCar();
        Mock.Mock<IBleService>()
            .Setup(b => b.GetPresence(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<List<string>>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new DtoBlePresenceResult
            {
                ScannerRunning = false,
                WarmingUp = false,
                MaxAgeMs = 90000,
                Vehicles = new List<DtoBlePresenceVehicle>
                {
                    new() { Vin = TestVin, Heard = false, LastSeenMsAgo = 600000, },
                },
            });

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Mock.Mock<IBlePresenceStateService>().Verify(p => p.RegisterPresenceAge(dtoCar.Id, null, It.IsAny<TimeSpan>()), Times.Once);
        Assert.Null(dtoCar.IsHomeGeofence.Value);
    }

    /// <summary>
    /// A car is present because a command reached it, even though it has not advertised for minutes - which is the
    /// normal state of a polled car, because our own connection silences it.
    /// </summary>
    [Fact]
    public async Task ACarKeptPresentByCommandEvidenceIsStillRead()
    {
        var dtoCar = SetupBleDataCollectionCar();
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.BlePresenceMaxAgeSeconds()).Returns(90);
        Mock.Mock<IBleService>()
            .Setup(b => b.GetPresence(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<List<string>>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new DtoBlePresenceResult
            {
                ScannerRunning = true,
                WarmingUp = false,
                MaxAgeMs = 90000,
                LastAdvertisementMsAgo = 200,
                Vehicles = new List<DtoBlePresenceVehicle>
                {
                    new()
                    {
                        Vin = TestVin,
                        Heard = true,
                        LastSeenMsAgo = 5000,
                        //Silent on the radio for two minutes, yet present: the command evidence is what carries it.
                        LastAdvertisementMsAgo = 120000,
                        LastCommandSuccessMsAgo = 5000,
                        LastSource = "command",
                    },
                },
            });
        Mock.Mock<IBlePresenceStateService>()
            .Setup(p => p.RegisterPresenceAge(It.IsAny<int>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan>()))
            .Returns(BlePresenceDecision.Present);
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, ResultMessage = AsleepBodyControllerStateJson });

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Assert.True(dtoCar.IsHomeGeofence.Value);
        Mock.Mock<IBlePresenceStateService>().Verify(p => p.RegisterPresenceAge(dtoCar.Id,
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(90)), Times.Once);
    }

    private const string SecondVin = "TESTVIN123456789B";
    private const string BleHost = "http://192.168.1.38:7210";
    private const string RadioA = "A0:AD:9F:79:AD:13";
    private const string RadioB = "BB:BB:BB:BB:BB:BB";

    /// <summary>
    /// Two BLE data collection cars with the given container URLs and adapters, both read as present whenever the
    /// container answers. Presence answers themselves are set up per test.
    /// </summary>
    private (DtoCar First, DtoCar Second) SetupTwoBleDataCollectionCars(string? firstHost, string? firstAdapter,
        string? secondHost, string? secondAdapter)
    {
        var first = SetupBleDataCollectionCar();
        first.BleApiBaseUrl = firstHost;
        first.BleAdapterAddress = firstAdapter;
        Context.Cars.Add(new Car
        {
            Id = 2,
            Vin = SecondVin,
            CarType = CarType.Tesla,
            ShouldBeManaged = true,
            UseBle = true,
            UseFleetTelemetry = false,
            IncludeTrackingRelevantFields = false,
        });
        Context.SaveChangesAsync().GetAwaiter().GetResult();
        DetachAllEntities();
        var second = new DtoCar
        {
            Id = 2,
            Vin = SecondVin,
            BleApiBaseUrl = secondHost,
            BleAdapterAddress = secondAdapter,
        };
        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar> { first, second });
        Mock.Mock<IBlePresenceStateService>()
            .Setup(p => p.RegisterPresenceAge(It.IsAny<int>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan>()))
            .Returns(BlePresenceDecision.Present);
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(It.IsAny<string>()))
            .ReturnsAsync(new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, ResultMessage = AsleepBodyControllerStateJson });
        return (first, second);
    }

    /// <summary>
    /// A container answer that heard both cars just now, on the adapter the container resolved.
    /// </summary>
    private static DtoBlePresenceResult PresenceHeardOn(string? resolvedAdapter) => new()
    {
        Adapter = resolvedAdapter,
        ScannerRunning = true,
        WarmingUp = false,
        MaxAgeMs = 90000,
        LastAdvertisementMsAgo = 120,
        Vehicles = new List<DtoBlePresenceVehicle>
        {
            new() { Vin = TestVin, Heard = true, LastSeenMsAgo = 300, LastSource = "advertisement", },
            new() { Vin = SecondVin, Heard = true, LastSeenMsAgo = 300, LastSource = "advertisement", },
        },
    };

    private void SetupPresenceAnswer(string? requestedAdapter, DtoBlePresenceResult answer)
    {
        Mock.Mock<IBleService>()
            .Setup(b => b.GetPresence(It.IsAny<string?>(), It.Is<string?>(a => a == requestedAdapter), It.IsAny<List<string>>(),
                It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(answer);
    }

    private void VerifyPresenceRequestedOnce(string? requestedAdapter)
    {
        Mock.Mock<IBleService>().Verify(b => b.GetPresence(It.IsAny<string?>(), It.Is<string?>(a => a == requestedAdapter),
            It.IsAny<List<string>>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Once);
    }

    /// <summary>
    /// Keeps the read of the first car running until the returned source is completed, like a slow connect.
    /// </summary>
    private TaskCompletionSource<DtoBleCommandResult> BlockBodyControllerRead(string vin)
    {
        var read = new TaskCompletionSource<DtoBleCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(vin)).Returns(read.Task);
        return read;
    }

    private static DtoBleCommandResult AsleepRead() =>
        new() { Success = true, Outcome = BleCommandOutcome.Ok, ResultMessage = AsleepBodyControllerStateJson };

    /// <summary>
    /// The instance a further radio group is handed to, resolved from the scope the refresh creates for it.
    /// </summary>
    private Mock<IServiceScope> SetupScopedService(IBleVehicleDataService scopedService)
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(p => p.GetService(typeof(IBleVehicleDataService))).Returns(scopedService);
        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);
        Mock.Mock<IServiceScopeFactory>().Setup(f => f.CreateScope()).Returns(scope.Object);
        return scope;
    }

    /// <summary>
    /// The customer setup behind the false read timeouts: one car without a selected adapter, one with the adapter
    /// the container uses by default anyway. Two configured groups, one radio - their reads must queue in TSC, not in
    /// the container where a read behind a slow connect runs out of TSC's HTTP timeout.
    /// </summary>
    [Fact]
    public async Task CarsOnOneRadioAreNeverReadInParallel()
    {
        SetupTwoBleDataCollectionCars(BleHost, null, BleHost + "/", RadioA);
        SetupPresenceAnswer(null, PresenceHeardOn(RadioA));
        SetupPresenceAnswer(RadioA, PresenceHeardOn(RadioA));
        var firstRead = BlockBodyControllerRead(TestVin);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        var refresh = service.RefreshBleCarData();

        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(TestVin), Times.Once);
        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(SecondVin), Times.Never);
        firstRead.SetResult(AsleepRead());
        await refresh;

        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(SecondVin), Times.Once);
        Mock.Mock<IServiceScopeFactory>().Verify(f => f.CreateScope(), Times.Never);
        //The presence answer of phase 1 is reused, never fetched again for the read.
        VerifyPresenceRequestedOnce(null);
        VerifyPresenceRequestedOnce(RadioA);
    }

    [Fact]
    public async Task CarsOnDifferentRadiosAreReadInParallelInTheirOwnScope()
    {
        SetupTwoBleDataCollectionCars(BleHost, RadioA, BleHost, RadioB);
        SetupPresenceAnswer(RadioA, PresenceHeardOn(RadioA));
        SetupPresenceAnswer(RadioB, PresenceHeardOn(RadioB));
        var firstRead = BlockBodyControllerRead(TestVin);
        //A real instance, as a scope would resolve it, so the read of the second radio actually happens.
        var scope = SetupScopedService(Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>());

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        var refresh = service.RefreshBleCarData();

        //The second radio is read while the first one is still busy.
        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(SecondVin), Times.Once);
        Mock.Mock<IServiceScopeFactory>().Verify(f => f.CreateScope(), Times.Once);
        firstRead.SetResult(AsleepRead());
        await refresh;

        scope.Verify(s => s.Dispose(), Times.Once);
        VerifyPresenceRequestedOnce(RadioA);
        VerifyPresenceRequestedOnce(RadioB);
    }

    [Fact]
    public async Task ASecondRadioGroupIsHandedItsGroupsWithTheirPresence()
    {
        var (_, second) = SetupTwoBleDataCollectionCars(BleHost, RadioA, BleHost, RadioB);
        SetupPresenceAnswer(RadioA, PresenceHeardOn(RadioA));
        var secondAnswer = PresenceHeardOn(RadioB);
        SetupPresenceAnswer(RadioB, secondAnswer);
        var scopedService = new Mock<IBleVehicleDataService>();
        SetupScopedService(scopedService.Object);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        scopedService.Verify(s => s.RefreshRadioGroupSafely(It.Is<List<DtoBleGroupPresence>>(groups =>
            groups.Count == 1
            && groups[0].Host == BleHost
            && groups[0].Adapter == RadioB
            && groups[0].Cars.Single() == second
            && groups[0].Presence == secondAnswer
            && groups[0].PresenceException == null)), Times.Once);
    }

    public enum PresenceFailure
    {
        Throws,
        ErrorMessage,
        AdapterNotFound,
    }

    /// <summary>
    /// A presence answer that does not name the radio tells nothing about which radio the group uses. Guessing wrong
    /// reads one radio in parallel, so everything on that container is serialized - and the failure is still reported
    /// as before.
    /// </summary>
    [Theory]
    [InlineData(PresenceFailure.Throws)]
    [InlineData(PresenceFailure.ErrorMessage)]
    [InlineData(PresenceFailure.AdapterNotFound)]
    public async Task AGroupWithoutAnswerIsSerializedWithItsContainerAndStillReported(PresenceFailure failure)
    {
        SetupTwoBleDataCollectionCars(BleHost, null, BleHost, RadioB);
        SetupPresenceAnswer(null, PresenceHeardOn(RadioA));
        var secondPresence = Mock.Mock<IBleService>()
            .Setup(b => b.GetPresence(It.IsAny<string?>(), It.Is<string?>(a => a == RadioB), It.IsAny<List<string>>(),
                It.IsAny<int?>(), It.IsAny<int?>()));
        switch (failure)
        {
            case PresenceFailure.Throws:
                secondPresence.ThrowsAsync(new HttpRequestException("container unreachable"));
                break;
            case PresenceFailure.ErrorMessage:
                secondPresence.ReturnsAsync(new DtoBlePresenceResult { ErrorMessage = "hci1 is gone", });
                break;
            case PresenceFailure.AdapterNotFound:
                secondPresence.ReturnsAsync(new DtoBlePresenceResult
                {
                    Adapter = RadioB,
                    ErrorMessage = $"The configured Bluetooth adapter {RadioB} is not present on this host.",
                });
                break;
        }

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Mock.Mock<IServiceScopeFactory>().Verify(f => f.CreateScope(), Times.Never);
        VerifyPresenceRequestedOnce(null);
        VerifyPresenceRequestedOnce(RadioB);
        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(TestVin), Times.Once);
        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(SecondVin), Times.Never);
        var expectedIssueKey = failure == PresenceFailure.AdapterNotFound
            ? Mock.Create<IIssueKeys>().BleAdapterNotFound
            : Mock.Create<IIssueKeys>().BleDataCollectionError;
        Mock.Mock<IErrorHandlingService>().Verify(e => e.HandleError(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), expectedIssueKey, SecondVin, It.IsAny<string?>()), Times.Once);
    }

    /// <summary>
    /// A container too old to report the resolved adapter: the two groups may share a radio, so they are read one
    /// after the other.
    /// </summary>
    [Fact]
    public async Task AnAnswerWithoutResolvedAdapterIsSerializedWithItsContainer()
    {
        SetupTwoBleDataCollectionCars(BleHost, RadioA, BleHost, RadioB);
        SetupPresenceAnswer(RadioA, PresenceHeardOn(RadioA));
        SetupPresenceAnswer(RadioB, PresenceHeardOn(null));
        var firstRead = BlockBodyControllerRead(TestVin);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        var refresh = service.RefreshBleCarData();

        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(SecondVin), Times.Never);
        firstRead.SetResult(AsleepRead());
        await refresh;

        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(SecondVin), Times.Once);
        Mock.Mock<IServiceScopeFactory>().Verify(f => f.CreateScope(), Times.Never);
    }

    [Fact]
    public async Task OneRadioIsTrackedUnderOneSilenceKey()
    {
        SetupTwoBleDataCollectionCars(BleHost, null, BleHost, RadioA.ToLowerInvariant());
        SetupPresenceAnswer(null, PresenceHeardOn(RadioA));
        SetupPresenceAnswer(RadioA, PresenceHeardOn(RadioA));

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Mock.Mock<IBlePresenceStateService>().Verify(p => p.RegisterRadioEvidence(RadioA, true,
            It.IsAny<DateTimeOffset>()), Times.Exactly(2));
        Mock.Mock<IBlePresenceStateService>().Verify(p => p.RegisterRadioEvidence(
            It.Is<string>(k => k != RadioA), It.IsAny<bool>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    /// <summary>
    /// One container reached once by DNS name and once by IP address: the hosts differ, the radio does not.
    /// </summary>
    [Fact]
    public async Task OneContainerUnderTwoHostNamesIsStillOneRadio()
    {
        SetupTwoBleDataCollectionCars("http://bleapi:7210", null, BleHost, null);
        SetupPresenceAnswer(null, PresenceHeardOn(RadioA));
        var firstRead = BlockBodyControllerRead(TestVin);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        var refresh = service.RefreshBleCarData();

        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(SecondVin), Times.Never);
        firstRead.SetResult(AsleepRead());
        await refresh;

        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(SecondVin), Times.Once);
        Mock.Mock<IServiceScopeFactory>().Verify(f => f.CreateScope(), Times.Never);
    }

    /// <summary>
    /// "hci:hci0" only names an adapter on its own container, so two containers reporting it are still two radios.
    /// </summary>
    [Fact]
    public async Task AdapterKeysWithoutBluetoothAddressKeepTheirContainerApart()
    {
        SetupTwoBleDataCollectionCars(BleHost, null, "http://192.168.1.39:7210", null);
        SetupPresenceAnswer(null, PresenceHeardOn("hci:hci0"));
        var scopedService = new Mock<IBleVehicleDataService>();
        SetupScopedService(scopedService.Object);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        scopedService.Verify(s => s.RefreshRadioGroupSafely(It.Is<List<DtoBleGroupPresence>>(groups =>
            groups.Count == 1 && groups[0].Host == "http://192.168.1.39:7210")), Times.Once);
    }

    [Theory]
    [InlineData(BleHost, RadioA, RadioA)]
    [InlineData("http://bleapi:7210", RadioA, RadioA)]
    [InlineData(BleHost, "HCI:HCI0", BleHost + "|HCI:HCI0")]
    [InlineData(BleHost, "DEFAULT", BleHost + "|DEFAULT")]
    [InlineData(BleHost, null, BleHost + "|")]
    [InlineData(BleHost, "A0:AD:9F:79:AD", BleHost + "|A0:AD:9F:79:AD")]
    [InlineData(BleHost, "A0:AD:9F:79:AD:13:14", BleHost + "|A0:AD:9F:79:AD:13:14")]
    public void RadioKeyIsTheBluetoothAddressAloneWhenThereIsOne(string? host, string? adapter, string expected)
    {
        Assert.Equal(expected, TeslaSolarCharger.Server.Services.BleVehicleDataService.RadioKey(host, adapter));
    }

    [Fact]
    public async Task ASingleGroupNeedsNoScopeOfItsOwn()
    {
        SetupBleDataCollectionCar();
        SetupPresence(present: false);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Mock.Mock<IServiceScopeFactory>().Verify(f => f.CreateScope(), Times.Never);
        Mock.Mock<IBleService>().Verify(b => b.GetPresence(It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<List<string>>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Once);
    }

    /// <summary>
    /// Seen at a customer: one car entered the container with a trailing slash, the other without. The configured
    /// groups must not split one container and adapter in two.
    /// </summary>
    [Theory]
    [InlineData(BleHost, BleHost + "/", "aa:bb:cc:dd:ee:ff", "AA:BB:CC:DD:EE:FF")]
    [InlineData(BleHost + "/", " " + BleHost, null, "")]
    [InlineData(BleHost, BleHost + "//", " AA:BB:CC:DD:EE:FF ", "AA:BB:CC:DD:EE:FF")]
    public async Task CarsOnTheSameContainerAndAdapterShareOneGroup(string firstHost, string secondHost,
        string? firstAdapter, string? secondAdapter)
    {
        SetupTwoBleDataCollectionCars(firstHost, firstAdapter, secondHost, secondAdapter);
        SetupPresence(present: false);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Mock.Mock<IServiceScopeFactory>().Verify(f => f.CreateScope(), Times.Never);
        Mock.Mock<IBleService>().Verify(b => b.GetPresence(BleHost, It.IsAny<string?>(),
            It.Is<List<string>>(v => v.SequenceEqual(new[] { TestVin, SecondVin })), It.IsAny<int?>(), It.IsAny<int?>()), Times.Once);
    }

    /// <summary>
    /// Different containers never share a radio, even when neither can say which adapter it resolved.
    /// </summary>
    [Fact]
    public async Task CarsOnDifferentContainersAreDifferentRadioGroups()
    {
        SetupTwoBleDataCollectionCars(BleHost, null, "http://192.168.1.39:7210", null);
        SetupPresenceAnswer(null, PresenceHeardOn(null));
        var scopedService = new Mock<IBleVehicleDataService>();
        SetupScopedService(scopedService.Object);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        scopedService.Verify(s => s.RefreshRadioGroupSafely(It.Is<List<DtoBleGroupPresence>>(groups =>
            groups.Count == 1 && groups[0].Host == "http://192.168.1.39:7210")), Times.Once);
    }

    /// <summary>
    /// A failing configured group used to fail the whole job. On one radio the next group must still run.
    /// </summary>
    [Fact]
    public async Task AFailingGroupDoesNotStopTheNextGroupOnTheSameRadio()
    {
        SetupTwoBleDataCollectionCars(BleHost, null, BleHost, RadioA);
        SetupPresenceAnswer(null, PresenceHeardOn(RadioA));
        SetupPresenceAnswer(RadioA, PresenceHeardOn(RadioA));
        Mock.Mock<IErrorHandlingService>()
            .Setup(e => e.HandleErrorResolved(It.IsAny<string>(), TestVin))
            .ThrowsAsync(new InvalidOperationException("database gone"));

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        var exception = await Record.ExceptionAsync(() => service.RefreshBleCarData());

        Assert.Null(exception);
        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(SecondVin), Times.Once);
    }

    [Fact]
    public async Task AFailingRadioGroupNeitherThrowsNorStopsTheOtherRadioGroups()
    {
        SetupTwoBleDataCollectionCars(BleHost, RadioA, BleHost, RadioB);
        SetupPresenceAnswer(RadioA, PresenceHeardOn(RadioA));
        SetupPresenceAnswer(RadioB, PresenceHeardOn(RadioB));
        var scopedService = new Mock<IBleVehicleDataService>();
        SetupScopedService(scopedService.Object);
        Mock.Mock<IErrorHandlingService>()
            .Setup(e => e.HandleErrorResolved(It.IsAny<string>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("database gone"));

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        var exception = await Record.ExceptionAsync(() => service.RefreshBleCarData());

        Assert.Null(exception);
        scopedService.Verify(s => s.RefreshRadioGroupSafely(It.IsAny<List<DtoBleGroupPresence>>()), Times.Once);
    }

    [Fact]
    public async Task AScopeThatCannotBeCreatedDoesNotStopTheFirstRadioGroup()
    {
        SetupTwoBleDataCollectionCars(BleHost, RadioA, BleHost, RadioB);
        SetupPresenceAnswer(RadioA, PresenceHeardOn(RadioA));
        SetupPresenceAnswer(RadioB, PresenceHeardOn(RadioB));
        Mock.Mock<IServiceScopeFactory>().Setup(f => f.CreateScope()).Throws(new ObjectDisposedException("provider"));

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        var exception = await Record.ExceptionAsync(() => service.RefreshBleCarData());

        Assert.Null(exception);
        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(TestVin), Times.Once);
    }

    [Fact]
    public async Task RefreshRadioGroupSafelySwallowsAFailure()
    {
        Mock.Mock<IErrorHandlingService>()
            .Setup(e => e.HandleErrorResolved(It.IsAny<string>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("A second operation was started on this context instance"));

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        var exception = await Record.ExceptionAsync(() => service.RefreshRadioGroupSafely(new List<DtoBleGroupPresence>
        {
            new()
            {
                Host = BleHost,
                Cars = new List<DtoCar> { new() { Id = 1, Vin = TestVin, }, },
                Presence = new DtoBlePresenceResult { MaxAgeMs = 90000, LastAdvertisementMsAgo = 100, },
            },
        }));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(false, null, RadioA, RadioA)]
    [InlineData(false, null, "a0:ad:9f:79:ad:13", RadioA)]
    [InlineData(false, null, null, null)]
    [InlineData(false, null, "", null)]
    [InlineData(false, "adapter gone", RadioA, null)]
    [InlineData(true, null, RadioA, null)]
    public void ResolvedAdapterIsOnlyTakenFromASuccessfulAnswer(bool presenceThrew, string? errorMessage,
        string? answeredAdapter, string? expected)
    {
        var group = new DtoBleGroupPresence
        {
            Presence = presenceThrew ? null : new DtoBlePresenceResult { Adapter = answeredAdapter, ErrorMessage = errorMessage, },
            PresenceException = presenceThrew ? new HttpRequestException() : null,
        };
        Assert.Equal(expected, TeslaSolarCharger.Server.Services.BleVehicleDataService.ResolvedAdapter(group));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("/", null)]
    [InlineData(BleHost, BleHost)]
    [InlineData(BleHost + "/", BleHost)]
    [InlineData(" " + BleHost + "/ ", BleHost)]
    public void NormalizesBleHost(string? host, string? expected)
    {
        Assert.Equal(expected, TeslaSolarCharger.Server.Services.BleVehicleDataService.NormalizeBleHost(host));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData("a0:ad:9f:79:ad:13", "A0:AD:9F:79:AD:13")]
    [InlineData(" A0:AD:9F:79:AD:13 ", "A0:AD:9F:79:AD:13")]
    public void NormalizesBleAdapter(string? adapter, string? expected)
    {
        Assert.Equal(expected, TeslaSolarCharger.Server.Services.BleVehicleDataService.NormalizeBleAdapter(adapter));
    }
}
