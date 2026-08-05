using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autofac.Extras.Moq;
using Moq;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using Xunit;


namespace TeslaSolarCharger.Tests.Services.Server.ChargingServiceV2;

public class SetCurrentOfNonChargingTeslasToMaxTests : TestBase
{
    public SetCurrentOfNonChargingTeslasToMaxTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Theory]
    // 1. Happy Path: All conditions met
    [InlineData(true, true, true, 10, 16, 16, false, ChargeModeV2.Auto, true)]

    // 2. IsOnline check
    [InlineData(false, true, true, 10, 16, 16, false, ChargeModeV2.Auto, false)]
    [InlineData(null, true, true, 10, 16, 16, false, ChargeModeV2.Auto, false)]

    // 3. IsHomeGeofence check
    [InlineData(true, false, true, 10, 16, 16, false, ChargeModeV2.Auto, false)]
    [InlineData(true, null, true, 10, 16, 16, false, ChargeModeV2.Auto, false)]

    // 4. PluggedIn check
    [InlineData(true, true, false, 10, 16, 16, false, ChargeModeV2.Auto, false)]
    [InlineData(true, true, null, 10, 16, 16, false, ChargeModeV2.Auto, false)]

    // 5. ChargerRequestedCurrent != MaximumAmpere check
    [InlineData(true, true, true, 16, 16, 16, false, ChargeModeV2.Auto, false)] // Requested == Max -> False
    [InlineData(true, true, true, null, 16, 16, false, ChargeModeV2.Auto, false)] // Requested null -> False (fails second check: Pilot > Requested)

    // 6. ChargerPilotCurrent > ChargerRequestedCurrent check
    [InlineData(true, true, true, 10, 16, 10, false, ChargeModeV2.Auto, false)] // Pilot == Requested -> False
    [InlineData(true, true, true, 10, 16, 9, false, ChargeModeV2.Auto, false)]  // Pilot < Requested -> False
    [InlineData(true, true, true, 10, 16, null, false, ChargeModeV2.Auto, false)] // Pilot null -> False

    // 7. IsCharging check
    [InlineData(true, true, true, 10, 16, 16, true, ChargeModeV2.Auto, false)] // Charging -> False
    [InlineData(true, true, true, 10, 16, 16, null, ChargeModeV2.Auto, false)] // Charging null -> IsCharging.Value==false is False -> Expect False

    // 8. ChargeModeV2 check
    [InlineData(true, true, true, 10, 16, 16, false, ChargeModeV2.Manual, false)]
    [InlineData(true, true, true, 10, 16, 16, false, ChargeModeV2.Off, false)]
    [InlineData(true, true, true, 10, 16, 16, false, ChargeModeV2.MaxPower, false)]
    public async Task SetCurrentOfNonChargingTeslasToMax_VariousConditions_SetsAmpCorrectly(
        bool? isOnline,
        bool? isHomeGeofence,
        bool? pluggedIn,
        int? chargerRequestedCurrent,
        int maximumAmpere,
        int? chargerPilotCurrent,
        bool? isCharging,
        ChargeModeV2 chargeModeV2,
        bool expectCall)
    {
        // Arrange
        var carId = 1;
        var car = new DtoCar
        {
            Id = carId,
            IsOnline = new DtoTimeStampedValue<bool?>(DateTimeOffset.MinValue, isOnline),
            IsHomeGeofence = new DtoTimeStampedValue<bool?>(DateTimeOffset.MinValue, isHomeGeofence),
            PluggedIn = new DtoTimeStampedValue<bool?>(DateTimeOffset.MinValue, pluggedIn),
            ChargerRequestedCurrent = new DtoTimeStampedValue<int?>(DateTimeOffset.MinValue, chargerRequestedCurrent),
            MaximumAmpere = maximumAmpere,
            ChargerPilotCurrent = new DtoTimeStampedValue<int?>(DateTimeOffset.MinValue, chargerPilotCurrent),
            IsCharging = new DtoTimeStampedValue<bool?>(DateTimeOffset.MinValue, isCharging),
            ChargeModeV2 = chargeModeV2,
            ShouldBeManaged = true
        };

        Mock.Mock<ISettings>()
            .Setup(s => s.CarsToManage)
            .Returns(new List<DtoCar> { car });

        var teslaServiceMock = Mock.Mock<ITeslaService>();
        teslaServiceMock
            .Setup(t => t.SetAmp(carId, maximumAmpere))
            .Returns(Task.CompletedTask);

        // Explicitly create the service to avoid ambiguity
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();

        // Act
        await service.SetCurrentOfNonChargingTeslasToMax();

        // Assert
        if (expectCall)
        {
            teslaServiceMock.Verify(t => t.SetAmp(carId, maximumAmpere), Times.Once);
        }
        else
        {
            teslaServiceMock.Verify(t => t.SetAmp(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }
    }

    [Fact]
    public async Task SetCurrentOfNonChargingTeslasToMax_BlePresenceUncertain_DoesNotSendCommand()
    {
        // Arrange: all filter conditions are met, but the BLE presence of the car is uncertain.
        var carId = 1;
        var car = new DtoCar
        {
            Id = carId,
            IsOnline = new DtoTimeStampedValue<bool?>(DateTimeOffset.MinValue, true),
            IsHomeGeofence = new DtoTimeStampedValue<bool?>(DateTimeOffset.MinValue, true),
            PluggedIn = new DtoTimeStampedValue<bool?>(DateTimeOffset.MinValue, true),
            ChargerRequestedCurrent = new DtoTimeStampedValue<int?>(DateTimeOffset.MinValue, 10),
            MaximumAmpere = 16,
            ChargerPilotCurrent = new DtoTimeStampedValue<int?>(DateTimeOffset.MinValue, 16),
            IsCharging = new DtoTimeStampedValue<bool?>(DateTimeOffset.MinValue, false),
            ChargeModeV2 = ChargeModeV2.Auto,
            ShouldBeManaged = true,
        };

        Mock.Mock<ISettings>()
            .Setup(s => s.CarsToManage)
            .Returns(new List<DtoCar> { car });
        Mock.Mock<IBlePresenceStateService>()
            .Setup(s => s.IsPresenceUncertain(carId))
            .Returns(true);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();

        // Act
        await service.SetCurrentOfNonChargingTeslasToMax();

        // Assert: the car may already have left home, so no command is sent while presence is uncertain.
        Mock.Mock<ITeslaService>().Verify(t => t.SetAmp(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }
}
