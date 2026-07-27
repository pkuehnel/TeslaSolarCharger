using Autofac;
using Autofac.Core;
using Moq;
using System;
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
    public void DetectsCarOutOfBleRangeResults()
    {
        Assert.True(TeslaSolarCharger.Server.Services.BleVehicleDataService.IsCarOutOfBleRangeResult(new DtoBleCommandResult
        {
            Success = false,
            ResultMessage = $"Error: failed to find BLE beacon for {TestVin} (S1a87a5a75f3df858C)",
        }));
        //Real world result of a BLE container when the car is not in range (verified on 2026-07-19).
        Assert.True(TeslaSolarCharger.Server.Services.BleVehicleDataService.IsCarOutOfBleRangeResult(new DtoBleCommandResult
        {
            Success = false,
            ResultMessage = "Error: context deadline exceeded",
        }));
        Assert.False(TeslaSolarCharger.Server.Services.BleVehicleDataService.IsCarOutOfBleRangeResult(new DtoBleCommandResult
        {
            Success = false,
            ResultMessage = "PrivateKeyPath is not set in the configuration",
        }));
        Assert.False(TeslaSolarCharger.Server.Services.BleVehicleDataService.IsCarOutOfBleRangeResult(new DtoBleCommandResult
        {
            Success = false,
            ResultMessage = null,
        }));
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
        Assert.Equal("Stopped", TeslaSolarCharger.Server.Services.BleVehicleDataService.GetChargingStateName(chargeState.ChargingState));
    }

    [Fact]
    public async Task OutOfRangeResultsBelowThresholdKeepLastKnownState()
    {
        var dtoCar = SetupBleDataCollectionCar();
        MockCurrentTime();
        var lastKnownTimestamp = CurrentFakeDate.AddHours(-1);
        dtoCar.IsHomeGeofence.Update(lastKnownTimestamp, true);
        dtoCar.IsOnline.Update(lastKnownTimestamp, true);
        dtoCar.PluggedIn.Update(lastKnownTimestamp, true);
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = false, ResultMessage = $"Error: failed to find BLE beacon for {TestVin}" });

        var (service, presenceStateService) = CreateServiceWithRealPresenceTracking();
        for (var i = 0; i < 4; i++)
        {
            await service.RefreshBleCarData();
        }

        //A transient BLE failure burst must not flip the car to away: the last known state stays valid.
        Assert.True(dtoCar.IsHomeGeofence.Value);
        Assert.True(dtoCar.IsOnline.Value);
        Assert.True(dtoCar.PluggedIn.Value);
        Assert.True(presenceStateService.IsPresenceUncertain(1));
        Assert.DoesNotContain(Context.CarValueLogs.ToList(), l => l.Type == CarValueType.LocatedAtHome && l.BooleanValue == false);
        //The sleep window must only be dropped once the car is confirmed away.
        Mock.Mock<IBleSleepWindowService>().Verify(s => s.ResetSleepWindow(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task FifthConsecutiveOutOfRangeResultMarksCarAwayAndResetsChargingValues()
    {
        var dtoCar = SetupBleDataCollectionCar();
        MockCurrentTime();
        var lastKnownTimestamp = CurrentFakeDate.AddHours(-1);
        dtoCar.IsHomeGeofence.Update(lastKnownTimestamp, true);
        dtoCar.IsOnline.Update(lastKnownTimestamp, true);
        dtoCar.PluggedIn.Update(lastKnownTimestamp, true);
        dtoCar.IsCharging.Update(lastKnownTimestamp, false);
        dtoCar.ChargerActualCurrent.Update(lastKnownTimestamp, 16);
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = false, ResultMessage = $"Error: failed to find BLE beacon for {TestVin}" });

        var (service, presenceStateService) = CreateServiceWithRealPresenceTracking(useRealPropertyUpdateHelper: true);
        for (var i = 0; i < 5; i++)
        {
            await service.RefreshBleCarData();
        }

        Assert.False(dtoCar.IsHomeGeofence.Value);
        Assert.False(dtoCar.IsOnline.Value);
        //The stale plug and charging values are reset so the car is not shown as "not at home but plugged in".
        Assert.False(dtoCar.PluggedIn.Value);
        Assert.False(dtoCar.IsCharging.Value);
        Assert.Equal(0, dtoCar.ChargerActualCurrent.Value);
        Assert.False(presenceStateService.IsPresenceUncertain(1));
        var carValueLogs = Context.CarValueLogs.ToList();
        Assert.Contains(carValueLogs, l => l.Type == CarValueType.LocatedAtHome && l.BooleanValue == false && l.Source == CarValueSource.Ble);
        Assert.Contains(carValueLogs, l => l.Type == CarValueType.AsleepOrOffline && l.BooleanValue == true && l.Source == CarValueSource.Ble);
        Assert.Contains(carValueLogs, l => l.Type == CarValueType.IsPluggedIn && l.BooleanValue == false && l.Source == CarValueSource.Estimation);
        Assert.Contains(carValueLogs, l => l.Type == CarValueType.IsCharging && l.BooleanValue == false && l.Source == CarValueSource.Estimation);
        Assert.Contains(carValueLogs, l => l.Type == CarValueType.ChargeAmps && l.IntValue == 0 && l.Source == CarValueSource.Estimation);
        Mock.Mock<IBleSleepWindowService>().Verify(s => s.ResetSleepWindow(1), Times.Once);
        Mock.Mock<IBleService>().Verify(b => b.GetChargeState(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AwayTransitionWritesValuesOnlyOnce()
    {
        var dtoCar = SetupBleDataCollectionCar();
        MockCurrentTime();
        dtoCar.IsHomeGeofence.Update(CurrentFakeDate.AddHours(-1), true);
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = false, ResultMessage = "Error: context deadline exceeded" });

        var (service, _) = CreateServiceWithRealPresenceTracking();
        for (var i = 0; i < 5; i++)
        {
            await service.RefreshBleCarData();
        }
        var carValueLogCountAfterConfirmation = Context.CarValueLogs.Count();

        //The car stays away: further out of range results must not write the same values again every poll.
        for (var i = 0; i < 3; i++)
        {
            await service.RefreshBleCarData();
        }

        Assert.False(dtoCar.IsHomeGeofence.Value);
        Assert.Equal(carValueLogCountAfterConfirmation, Context.CarValueLogs.Count());
    }

    [Fact]
    public async Task SuccessfulReadResetsAwayConfirmationProgress()
    {
        var dtoCar = SetupBleDataCollectionCar();
        MockCurrentTime();
        var bleServiceMock = Mock.Mock<IBleService>();
        var outOfRangeResult = new DtoBleCommandResult { Success = false, ResultMessage = "Error: context deadline exceeded" };
        bleServiceMock.Setup(b => b.GetBodyControllerState(TestVin)).ReturnsAsync(outOfRangeResult);

        var (service, presenceStateService) = CreateServiceWithRealPresenceTracking();
        for (var i = 0; i < 4; i++)
        {
            await service.RefreshBleCarData();
        }
        Assert.True(presenceStateService.IsPresenceUncertain(1));

        //One successful read proves the car is still at home and must reset the consecutive failure counter.
        bleServiceMock.Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = true, ResultMessage = AsleepBodyControllerStateJson });
        await service.RefreshBleCarData();
        Assert.False(presenceStateService.IsPresenceUncertain(1));
        Assert.True(dtoCar.IsHomeGeofence.Value);

        //The full number of consecutive failures is required again before the car is confirmed away.
        bleServiceMock.Setup(b => b.GetBodyControllerState(TestVin)).ReturnsAsync(outOfRangeResult);
        for (var i = 0; i < 4; i++)
        {
            await service.RefreshBleCarData();
        }
        Assert.True(dtoCar.IsHomeGeofence.Value);
        await service.RefreshBleCarData();
        Assert.False(dtoCar.IsHomeGeofence.Value);
    }

    [Fact]
    public async Task NonRangeErrorsDoNotCountTowardsAwayConfirmation()
    {
        var dtoCar = SetupBleDataCollectionCar();
        MockCurrentTime();
        dtoCar.IsHomeGeofence.Update(CurrentFakeDate.AddHours(-1), true);
        var bleServiceMock = Mock.Mock<IBleService>();
        var outOfRangeResult = new DtoBleCommandResult { Success = false, ResultMessage = "Error: context deadline exceeded" };
        bleServiceMock.Setup(b => b.GetBodyControllerState(TestVin)).ReturnsAsync(outOfRangeResult);

        var (service, presenceStateService) = CreateServiceWithRealPresenceTracking();
        for (var i = 0; i < 3; i++)
        {
            await service.RefreshBleCarData();
        }

        //An error that carries no presence information must neither increment nor reset the counter.
        bleServiceMock.Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = false, ResultMessage = "PrivateKeyPath is not set in the configuration" });
        await service.RefreshBleCarData();
        Assert.True(presenceStateService.IsPresenceUncertain(1));

        bleServiceMock.Setup(b => b.GetBodyControllerState(TestVin)).ReturnsAsync(outOfRangeResult);
        //Counter is at 3: if the unrelated error had counted, this fourth out of range result would confirm away.
        await service.RefreshBleCarData();
        Assert.True(dtoCar.IsHomeGeofence.Value);
        //If the unrelated error had reset the counter, this fifth out of range result would not confirm away yet.
        await service.RefreshBleCarData();
        Assert.False(dtoCar.IsHomeGeofence.Value);
    }

    [Fact]
    public async Task RefreshSingleCarDataSharesTheOutOfRangeCounter()
    {
        var dtoCar = SetupBleDataCollectionCar();
        MockCurrentTime();
        dtoCar.IsHomeGeofence.Update(CurrentFakeDate.AddHours(-1), true);
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = false, ResultMessage = "Error: context deadline exceeded" });

        var (service, _) = CreateServiceWithRealPresenceTracking();
        for (var i = 0; i < 4; i++)
        {
            await service.RefreshBleCarData();
        }
        Assert.True(dtoCar.IsHomeGeofence.Value);

        //The delayed post command refresh uses the same per car counter as the cycle refresh.
        await service.RefreshSingleCarData(1);
        Assert.False(dtoCar.IsHomeGeofence.Value);
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
    public async Task AwakeCarInSleepWindowKeepsPresenceButSkipsChargeState()
    {
        var dtoCar = SetupBleDataCollectionCar();
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = true, ResultMessage = AwakeBodyControllerStateJson });
        //Simulate an active sleep window: the infotainment charge state poll must be withheld.
        Mock.Mock<IBleSleepWindowService>()
            .Setup(s => s.ShouldPollInfotainment(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<int>()))
            .Returns(false);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        //Presence and online state are still updated from the VCSEC body controller state...
        Assert.True(dtoCar.IsHomeGeofence.Value);
        Assert.True(dtoCar.IsOnline.Value);
        //...but the infotainment charge state is not polled so the car can fall asleep.
        Mock.Mock<IBleService>().Verify(b => b.GetChargeState(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChargingCarSkipsBodyControllerAndReadsChargeStateOnly()
    {
        var dtoCar = SetupBleDataCollectionCar();
        //Car is known to be charging, so the VCSEC body controller call should be skipped.
        dtoCar.IsCharging.Update(DateTimeOffset.UtcNow, true);
        Mock.Mock<IBleService>().Setup(b => b.GetChargeState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = true, ResultMessage = ChargingChargeStateJson });

        //Use the real property update helper so the DtoCar properties are actually updated.
        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>(
            new TypedParameter(typeof(ICarPropertyUpdateHelper), Mock.Create<CarPropertyUpdateHelper>()));
        await service.RefreshBleCarData();

        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(It.IsAny<string>()), Times.Never);
        Mock.Mock<IBleService>().Verify(b => b.GetChargeState(TestVin), Times.Once);
        //Charging implies at home and online, both are set without a beacon call.
        Assert.True(dtoCar.IsHomeGeofence.Value);
        Assert.True(dtoCar.IsOnline.Value);
        Assert.Equal(55, dtoCar.SoC.Value);
        Assert.Equal(16, dtoCar.ChargerActualCurrent.Value);
        var carValueLogs = Context.CarValueLogs.ToList();
        Assert.Contains(carValueLogs, l => l.Type == CarValueType.LocatedAtHome && l.BooleanValue == true && l.Source == CarValueSource.Ble);
        Assert.Contains(carValueLogs, l => l.Type == CarValueType.AsleepOrOffline && l.BooleanValue == false && l.Source == CarValueSource.Ble);
    }

    [Fact]
    public async Task ChargingCarOutOfRangeFallsBackToBodyControllerAndCountsOncePerCycle()
    {
        var dtoCar = SetupBleDataCollectionCar();
        MockCurrentTime();
        var lastKnownTimestamp = CurrentFakeDate.AddHours(-1);
        dtoCar.IsHomeGeofence.Update(lastKnownTimestamp, true);
        dtoCar.IsCharging.Update(lastKnownTimestamp, true);
        //The charging state was stale: the car left BLE range, so the charge state read times out.
        Mock.Mock<IBleService>().Setup(b => b.GetChargeState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = false, ResultMessage = "Error: context deadline exceeded" });
        Mock.Mock<IBleService>().Setup(b => b.GetBodyControllerState(TestVin))
            .ReturnsAsync(new DtoBleCommandResult { Success = false, ResultMessage = $"Error: failed to find BLE beacon for {TestVin}" });

        var (service, presenceStateService) = CreateServiceWithRealPresenceTracking(useRealPropertyUpdateHelper: true);
        await service.RefreshBleCarData();

        //Falls back to the body controller state to correctly resolve presence / online.
        Mock.Mock<IBleService>().Verify(b => b.GetChargeState(TestVin), Times.Once);
        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(TestVin), Times.Once);
        //A single out of range cycle keeps the last known state but makes the presence uncertain.
        Assert.True(dtoCar.IsHomeGeofence.Value);
        Assert.True(presenceStateService.IsPresenceUncertain(1));

        //Even though both BLE reads fail as out of range, one poll cycle may only count one failure: with double
        //counting the car would already be confirmed away after three cycles.
        for (var i = 0; i < 3; i++)
        {
            await service.RefreshBleCarData();
        }
        Assert.True(dtoCar.IsHomeGeofence.Value);

        await service.RefreshBleCarData();
        Assert.False(dtoCar.IsHomeGeofence.Value);
        //The away transition also resets the stale charging state, so later cycles skip the charge state fast path.
        Assert.False(dtoCar.IsCharging.Value);
    }

    [Fact]
    public async Task SkipsReadWhenAnotherReadIsInProgress()
    {
        SetupBleDataCollectionCar();
        //Another read for this car is already running, so this read must be skipped entirely.
        Mock.Mock<IBleReadCoordinator>().Setup(c => c.TryBeginRead(It.IsAny<int>())).Returns(false);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>();
        await service.RefreshBleCarData();

        Mock.Mock<IBleService>().Verify(b => b.GetBodyControllerState(It.IsAny<string>()), Times.Never);
        Mock.Mock<IBleService>().Verify(b => b.GetChargeState(It.IsAny<string>()), Times.Never);
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

    private void MockCurrentTime()
    {
        Mock.Mock<IDateTimeProvider>().Setup(d => d.UtcNow()).Returns(CurrentFakeDate.UtcDateTime);
    }

    /// <summary>
    /// Creates the service with a real BlePresenceStateService so consecutive out of range results are actually
    /// counted across refresh calls. Optionally also uses the real property update helper so DtoCar properties that
    /// are updated via CarValueLogs (e.g. PluggedIn) are actually changed.
    /// </summary>
    private (TeslaSolarCharger.Server.Services.BleVehicleDataService Service, IBlePresenceStateService PresenceStateService)
        CreateServiceWithRealPresenceTracking(bool useRealPropertyUpdateHelper = false)
    {
        var presenceStateService = Mock.Create<TeslaSolarCharger.Server.Services.BlePresenceStateService>();
        var parameters = new List<Parameter> { new TypedParameter(typeof(IBlePresenceStateService), presenceStateService), };
        if (useRealPropertyUpdateHelper)
        {
            parameters.Add(new TypedParameter(typeof(ICarPropertyUpdateHelper), Mock.Create<CarPropertyUpdateHelper>()));
        }
        var service = Mock.Create<TeslaSolarCharger.Server.Services.BleVehicleDataService>(parameters.ToArray());
        return (service, presenceStateService);
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
        //By default let every read acquire the read slot, individual tests can override this.
        Mock.Mock<IBleReadCoordinator>().Setup(c => c.TryBeginRead(It.IsAny<int>())).Returns(true);
        //By default the sleep window never silences the infotainment poll (mirrors the real service before a window
        //starts). Individual tests can override this to simulate an active sleep window.
        Mock.Mock<IBleSleepWindowService>()
            .Setup(s => s.ShouldPollInfotainment(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<int>()))
            .Returns(true);
        return dtoCar;
    }
}
